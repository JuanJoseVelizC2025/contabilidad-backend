using System.Net.Http.Json;

namespace Contabilidad.Api.Services;

public class CorreoServicio
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    private readonly ILogger<CorreoServicio> _log;

    public CorreoServicio(IHttpClientFactory http, IConfiguration config, ILogger<CorreoServicio> log)
    {
        _http = http;
        _config = config;
        _log = log;
    }

    public async Task<bool> Enviar(string para, string nombre, string asunto, string html)
    {
        var clave = _config["Brevo:ApiKey"];
        var remitente = _config["Brevo:SenderEmail"];
        if (string.IsNullOrWhiteSpace(clave) || string.IsNullOrWhiteSpace(remitente))
        {
            _log.LogWarning("Brevo no esta configurado (falta ApiKey o SenderEmail)");
            return false;
        }

        try
        {
            var cliente = _http.CreateClient();
            using var peticion = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
            peticion.Headers.Add("api-key", clave);
            peticion.Headers.Add("accept", "application/json");
            peticion.Content = JsonContent.Create(new
            {
                sender = new { name = _config["Brevo:SenderName"] ?? "Contabilidad ERP", email = remitente },
                to = new[] { new { email = para, name = nombre } },
                subject = asunto,
                htmlContent = html
            });

            var respuesta = await cliente.SendAsync(peticion);
            if (!respuesta.IsSuccessStatusCode)
            {
                var cuerpo = await respuesta.Content.ReadAsStringAsync();
                _log.LogWarning("Brevo respondio {Codigo}: {Cuerpo}", (int)respuesta.StatusCode, cuerpo);
            }
            return respuesta.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "No se pudo enviar el correo");
            return false;
        }
    }
}