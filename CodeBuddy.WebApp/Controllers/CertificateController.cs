using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Mvc;

namespace CodeBuddy.WebApp.Controllers;

[ApiController]
[Route("/api/certs")]
public class CertificateController(X509Certificate2 certificate) : ControllerBase
{
    [HttpGet("hash")]
    public string GetCertHash()
    {
        return Convert.ToBase64String(SHA256.HashData(certificate.RawData));
    }
}