using System.Security.Cryptography;
using MongoDB.Bson;
using System.Text;
using Microsoft.AspNetCore.Http;
using dotCheck.Domain.Entities;

namespace dotCheck.Infrastructure.Security;

public sealed class HttpFingerprintService
{
    public UserLoginLog CreateLoginLog(ObjectId userId, HttpRequest request, DateTime loginAt)
    {
        var userAgent = GetHeader(request, "User-Agent");
        var accept = GetHeader(request, "Accept");
        var acceptLanguage = GetHeader(request, "Accept-Language");
        var acceptEncoding = GetHeader(request, "Accept-Encoding");
        var secChUa = GetHeader(request, "Sec-CH-UA");
        var secChUaMobile = GetHeader(request, "Sec-CH-UA-Mobile");
        var secChUaPlatform = GetHeader(request, "Sec-CH-UA-Platform");

        var fingerprintSource = string.Join('\n',
        [
            userAgent,
            accept,
            acceptLanguage,
            acceptEncoding,
            secChUa,
            secChUaMobile,
            secChUaPlatform
        ]);

        var fingerprint = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintSource)));

        return new UserLoginLog
        {
            Id = MongoDB.Bson.ObjectId.GenerateNewId(),
            UserId = userId,
            LoginAt = loginAt,
            IpAddress = request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
            UserAgent = userAgent,
            Accept = accept,
            AcceptLanguage = acceptLanguage,
            AcceptEncoding = acceptEncoding,
            SecChUa = secChUa,
            SecChUaMobile = secChUaMobile,
            SecChUaPlatform = secChUaPlatform,
            HttpFingerprint = fingerprint
        };
    }

    private static string GetHeader(HttpRequest request, string name) =>
        request.Headers.TryGetValue(name, out var value)
            ? value.ToString()
            : string.Empty;
}
