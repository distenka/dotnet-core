using System;
using System.Net;
using System.Net.Http;

namespace Distenka.Client;

public class DistenkaHttpRequestException : HttpRequestException
{
    public HttpStatusCode StatusCode { get; }
    public HttpMethod Method { get; }
    public Uri RequestUri { get; }
    public string Content { get; }

    public DistenkaHttpRequestException(HttpStatusCode statusCode, HttpMethod method, Uri requestUri, string content = null)
        : base($"Received HTTP {(int)statusCode} ({statusCode}) while {method}ing URL '{requestUri}'.{(content == null ? "" : Environment.NewLine + content)}")
    {
        StatusCode = statusCode;
        Method = method;
        RequestUri = requestUri;
        Content = content;
    }
}