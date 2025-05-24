using System.Security.Cryptography;

using SubscriptionServiceProcessor;

public class EncryptionService : IEncryptionService
{
	private readonly byte[] _key;
	private readonly byte[] _iv;

	public EncryptionService(string key, string iv)
	{
		_key = Convert.FromBase64String(key);
		_iv = Convert.FromBase64String(iv);
	}

	public string Encrypt(string plainText)
	{
		using (Aes aes = Aes.Create())
		{
			aes.Key = _key;
			aes.IV = _iv;
			ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
			using (MemoryStream ms = new MemoryStream())
			{
				using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
				{
					using (StreamWriter sw = new StreamWriter(cs))
					{
						sw.Write(plainText);
					}
					return Convert.ToBase64String(ms.ToArray());
				}
			}
		}
	}

	public string Decrypt(string cipherText)
	{
		using (Aes aes = Aes.Create())
		{
			aes.Key = _key;
			aes.IV = _iv;
			ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
			using (MemoryStream ms = new MemoryStream(Convert.FromBase64String(cipherText)))
			{
				using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
				{
					using (StreamReader sr = new StreamReader(cs))
					{
						return sr.ReadToEnd();
					}
				}
			}
		}
	}
}