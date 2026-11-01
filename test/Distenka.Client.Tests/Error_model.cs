using Distenka.Client.Internal;
using FluentAssertions;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Distenka.Client.Tests
{
	/// <summary>
	/// DCS-022: non-success responses become <see cref="DistenkaApiException"/>. Error bodies use the platform's documented
	/// shape <c>{ "code": …, "error": … }</c> (ADR-008 §1) with the codes recorded in the backlog @ 4c3cf2c; the sample
	/// texts are illustrative. Verbatim doc-example bodies are a pending check (docs/api-*.md not readable from this session).
	/// </summary>
	public class Error_model
	{
		static readonly DateTimeOffset Now = new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);

		static HttpResponseMessage Response(int status, string body = null, string mediaType = "application/json")
		{
			var response = new HttpResponseMessage((HttpStatusCode)status);
			if (body != null)
				response.Content = new StringContent(body, Encoding.UTF8, mediaType);
			return response;
		}

		static Task<DistenkaApiException> Read(HttpResponseMessage response) => ApiErrorReader.CreateAsync(response, Now, CancellationToken.None);

		public static TheoryData<string> AllCodes()
		{
			var data = new TheoryData<string>();
			foreach (var field in typeof(DistenkaErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
				data.Add((string)field.GetRawConstantValue());
			return data;
		}

		[Fact]
		public void the_documented_codes_have_constants()
		{
			// AC 1: one constant per documented code recorded in the backlog (DCS-022 cur, ADR-006 table).
			AllCodes().Select(row => (string)row[0]).Should().BeEquivalentTo(
				"idempotency_key_malformed", "idempotency_key_reuse", "idempotency_key_in_flight",
				"queue_depth_exceeded", "platform_backpressure", "platform_overloaded", "rate_limit_exceeded",
				"quota_exhausted", "run_already_ended", "scope_violation", "invocation_key_required",
				"run_capability_required", "processor_unavailable", "callbacks_unavailable", "capability_unavailable",
				// DCS-060 AC 4 (re-homed from this story), from docs/api-callbacks.md:
				"callback_not_https", "callback_userinfo", "callback_too_long", "callback_malformed", "callback_unresolvable",
				"callback_private_address");
		}

		[Theory]
		[MemberData(nameof(AllCodes))]
		public async Task each_code_in_a_body_maps_to_the_exception(string code)
		{
			var thrown = await Read(Response(400, $"{{\"code\":\"{code}\",\"error\":\"Sample text for {code}.\"}}"));

			thrown.Code.Should().Be(code);
			thrown.Error.Should().Be($"Sample text for {code}.");
			thrown.Message.Should().Contain(code).And.Contain($"Sample text for {code}.");
			thrown.Body.Should().NotBeNull();
		}

		[Fact]
		public async Task PascalCase_members_are_read_too()
		{
			var thrown = await Read(Response(409, "{\"Code\":\"idempotency_key_reuse\",\"Error\":\"Key reused.\"}"));

			thrown.Code.Should().Be(DistenkaErrorCodes.IdempotencyKeyReuse);
			thrown.Error.Should().Be("Key reused.");
		}

		[Fact]
		public async Task other_body_members_stay_readable_after_the_response_is_disposed()
		{
			DistenkaApiException thrown;
			using (var response = Response(402, "{\"code\":\"quota_exhausted\",\"error\":\"Quota exhausted.\",\"limit\":100}"))
				thrown = await Read(response);

			thrown.StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
			thrown.Body.Value.GetProperty("limit").GetInt32().Should().Be(100);
			thrown.IsRetryable.Should().BeFalse();
		}

		[Theory]
		[InlineData(401)]
		[InlineData(404)]
		public async Task responses_without_a_body_have_no_code(int status)
		{
			var thrown = await Read(Response(status));

			thrown.StatusCode.Should().Be((HttpStatusCode)status);
			thrown.Code.Should().BeNull();
			thrown.Body.Should().BeNull();
			thrown.RawContent.Should().BeNull();
			thrown.IsRetryable.Should().BeFalse("401 and 404 are terminal; a 401 is never retried with a new token");
		}

		[Fact]
		public async Task a_non_JSON_body_keeps_the_status_and_bounded_raw_content()
		{
			// AC 2: bounded to 4 KB.
			var page = "<html>" + new string('x', 10_000) + "</html>";

			var thrown = await Read(Response(502, page, "text/html"));

			thrown.StatusCode.Should().Be(HttpStatusCode.BadGateway);
			thrown.Code.Should().BeNull();
			thrown.Body.Should().BeNull();
			thrown.RawContent.Should().StartWith("<html>xxx").And.HaveLength(4096);
		}

		[Fact]
		public async Task a_JSON_body_that_is_not_an_object_is_raw_content_only()
		{
			var thrown = await Read(Response(500, "[\"not\",\"an\",\"object\"]"));

			thrown.Body.Should().BeNull();
			thrown.Code.Should().BeNull();
			thrown.RawContent.Should().Be("[\"not\",\"an\",\"object\"]");
		}

		[Fact]
		public async Task retry_after_in_seconds_is_parsed()
		{
			// AC 3, delta-seconds form.
			var response = Response(429, "{\"code\":\"rate_limit_exceeded\",\"error\":\"Slow down.\"}");
			response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(7));

			var thrown = await Read(response);

			thrown.RetryAfter.Should().Be(TimeSpan.FromSeconds(7));
			thrown.IsRetryable.Should().BeTrue();
		}

		[Fact]
		public async Task retry_after_as_an_http_date_is_measured_from_the_Date_header()
		{
			// AC 3, HTTP-date form.
			var response = Response(503, "{\"code\":\"capability_unavailable\",\"error\":\"Try later.\"}");
			response.Headers.Date = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
			response.Headers.RetryAfter = new RetryConditionHeaderValue(new DateTimeOffset(2026, 11, 1, 9, 0, 30, TimeSpan.Zero));

			(await Read(response)).RetryAfter.Should().Be(TimeSpan.FromSeconds(30));
		}

		[Fact]
		public async Task retry_after_as_an_http_date_without_a_Date_header_uses_the_clock()
		{
			var response = Response(429);
			response.Headers.TryAddWithoutValidation("Retry-After", "Sun, 01 Nov 2026 08:00:12 GMT");

			(await Read(response)).RetryAfter.Should().Be(TimeSpan.FromSeconds(12));
		}

		[Fact]
		public async Task a_retry_after_date_in_the_past_means_no_wait()
		{
			var response = Response(429);
			response.Headers.RetryAfter = new RetryConditionHeaderValue(Now.AddMinutes(-1));

			(await Read(response)).RetryAfter.Should().Be(TimeSpan.Zero);
		}

		[Theory]
		[InlineData(429, "rate_limit_exceeded", true)]
		[InlineData(429, "queue_depth_exceeded", true)]
		[InlineData(429, "platform_backpressure", true)]
		[InlineData(429, "platform_overloaded", true)]
		[InlineData(503, "capability_unavailable", true)]
		[InlineData(409, "idempotency_key_in_flight", true)]
		[InlineData(409, "idempotency_key_reuse", false)]
		[InlineData(409, "run_already_ended", false)]
		[InlineData(409, "processor_unavailable", false)]
		[InlineData(402, "quota_exhausted", false)]
		[InlineData(400, "idempotency_key_malformed", false)]
		[InlineData(403, "scope_violation", false)]
		[InlineData(403, "invocation_key_required", false)]
		[InlineData(403, "run_capability_required", false)]
		[InlineData(401, null, false)]
		[InlineData(404, null, false)]
		[InlineData(413, null, false)]
		[InlineData(500, null, false)]
		[InlineData(502, null, true)]
		[InlineData(504, null, true)]
		[InlineData(408, null, true)]
		public async Task retryability_follows_the_rule_table(int status, string code, bool retryable)
		{
			var body = code == null ? null : $"{{\"code\":\"{code}\",\"error\":\"x\"}}";

			(await Read(Response(status, body))).IsRetryable.Should().Be(retryable);
		}

		[Fact]
		public async Task the_message_never_contains_request_headers()
		{
			const string Token = "DK-0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
			var request = new HttpRequestMessage(HttpMethod.Post, "https://api.example/api/runs");
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
			var response = Response(401);
			response.RequestMessage = request;

			var thrown = await Read(response);

			thrown.Message.Should().NotContain(Token).And.NotContain("Bearer");
			thrown.ToString().Should().NotContain(Token);
		}

		[Fact]
		public async Task request_id_is_read_when_present()
		{
			var response = Response(500);
			response.Headers.TryAddWithoutValidation("X-Request-Id", "req-123");

			(await Read(response)).RequestId.Should().Be("req-123");
			(await Read(Response(500))).RequestId.Should().BeNull();
		}

		[Fact]
		public async Task EnsureSuccessAsync_passes_success_and_throws_otherwise()
		{
			await ApiErrorReader.EnsureSuccessAsync(Response(202, "{}"), CancellationToken.None);

			var ensure = () => ApiErrorReader.EnsureSuccessAsync(Response(409, "{\"code\":\"idempotency_key_reuse\",\"error\":\"Reused.\"}"), CancellationToken.None);

			(await ensure.Should().ThrowAsync<DistenkaApiException>()).Which.Code.Should().Be(DistenkaErrorCodes.IdempotencyKeyReuse);
		}

		[Fact]
		public async Task readable_error_text_is_bounded_in_the_message()
		{
			var thrown = await Read(Response(400, $"{{\"code\":\"x\",\"error\":\"{new string('e', 2000)}\"}}"));

			thrown.Message.Length.Should().BeLessThan(700);
			thrown.Error.Should().HaveLength(2000, "the full text stays available on the property");
		}

		[Fact]
		public void the_public_constructor_truncates_raw_content() =>
			new DistenkaApiException("m", HttpStatusCode.BadRequest, rawContent: new string('r', 5000)).RawContent.Should().HaveLength(4096);
	}
}
