using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sharphound
{
    public class AuthSigner : DelegatingHandler
    {
        private const string AuthorizationHeader = "Authorization";
        private const string DateHeader = "RequestDate";
        private const string SignatureHeader = "Signature";
        private const string AuthSignature = "bhesignature";

        private readonly string _token;
        private readonly string _tokenId;

        public AuthSigner(string token, string tokenId)
        {
            _token = token;
            _tokenId = tokenId;
            InnerHandler = new HttpClientHandler();
        }

        public AuthSigner(string token, string tokenId, HttpMessageHandler innerHandler) : base(innerHandler)
        {
            _token = token;
            _tokenId = tokenId;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // Remove old headers that might come from somewhere else
            request.Headers.Remove(AuthorizationHeader);
            request.Headers.Remove(DateHeader);
            request.Headers.Remove(SignatureHeader);

            // First we sign our request
            var time = DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:sszzz");
            //Console.WriteLine("Time: " + time);
            //Console.WriteLine("Formatted: " + time.Substring(0, 13));
            var opKey = SignOperationKey(_token, request.Method, request.RequestUri.AbsolutePath);
            //Console.WriteLine("opKey: " + EncodeBase64(opKey));
            var dateKey = SignDateKey(opKey, time);
            //Console.WriteLine("dateKey: " + EncodeBase64(dateKey));
            var bodyBytes = request.Content == null ? new byte[0] : await request.Content.ReadAsByteArrayAsync();
            var finalSignature = SignBody(dateKey, bodyBytes);
            request.Headers.Add(AuthorizationHeader, $"{AuthSignature} {_tokenId}");
            request.Headers.Add(DateHeader, time);
            request.Headers.Add(SignatureHeader, EncodeBase64(finalSignature));
            //Console.WriteLine("Signature: " + EncodeBase64(finalSignature));
            //Console.WriteLine("Token ID: " + _tokenId);
            //Console.WriteLine("Token Key: " + _token);

            return await base.SendAsync(request, cancellationToken);
        }

        private static HMACSHA256 CreateDigester(byte[] token)
        {
            return new HMACSHA256(token);
        }

        private static byte[] EncodeString(string toEncode)
        {
            return Encoding.UTF8.GetBytes(toEncode);
        }

        private static string EncodeBase64(byte[] signature)
        {
            return Convert.ToBase64String(signature);
        }

        private static byte[] SignOperationKey(string token, HttpMethod requestMethod, string requestURI)
        {
            var digester = CreateDigester(EncodeString(token));
            var content = EncodeString($"{requestMethod}{requestURI}");
            return digester.ComputeHash(content);
        }

        private static byte[] SignDateKey(byte[] opBytes, string formattedDate)
        {
            var digester = CreateDigester(opBytes);
            return digester.ComputeHash(EncodeString(formattedDate.Substring(0, 13)));
        }

        private static byte[] SignBody(byte[] dateBytes, byte[] content)
        {
            var digester = CreateDigester(dateBytes);
            return digester.ComputeHash(content);
        }
    }
}
