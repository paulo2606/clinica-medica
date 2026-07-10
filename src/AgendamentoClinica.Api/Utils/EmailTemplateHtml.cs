namespace AgendamentoClinica.Api.Utils;

public static class EmailTemplateHtml
{
    public static string MontarCartaoAcao(
        string eyebrow, string saudacao, string paragrafo1, string paragrafo2, string textoBotao, string link, string aviso) => $"""
        <!DOCTYPE html>
        <html lang="pt-BR"><head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>Clínica+Saúde</title>
        </head>
        <body style="margin:0;">
        <div style="width:600px;margin:0 auto;background:#f6f9f7;font-family:'Segoe UI',Helvetica,Arial,sans-serif;color:#282c29;">

          <div style="padding:36px 40px 28px 40px;text-align:center;">
            <div style="display:inline-flex;align-items:center;gap:10px;">
              <div style="width:34px;height:34px;border-radius:10px;background:#2f7a56;display:flex;align-items:center;justify-content:center;flex-shrink:0;">
                <div style="position:relative;width:16px;height:16px;">
                  <div style="position:absolute;top:7px;left:0;width:16px;height:2px;background:white;border-radius:1px;"></div>
                  <div style="position:absolute;top:0;left:7px;width:2px;height:16px;background:white;border-radius:1px;"></div>
                </div>
              </div>
              <span style="font-size:20px;font-weight:700;letter-spacing:-0.01em;color:#33453c;">Clínica<span style="color:#2f7a56;">+Saúde</span></span>
            </div>
          </div>

          <div style="background:white;border-radius:20px;margin:0 20px;box-shadow:0 1px 3px rgba(0,0,0,0.06);overflow:hidden;">
            <div style="height:6px;background:#2f7a56;"></div>
            <div style="padding:48px 44px 40px 44px;">
              <p style="margin:0 0 22px 0;font-size:15px;font-weight:600;letter-spacing:0.06em;text-transform:uppercase;color:#2f7a56;">{eyebrow}</p>
              <h1 style="margin:0 0 20px 0;font-size:28px;line-height:1.3;font-weight:700;letter-spacing:-0.01em;color:#232622;">Olá, <span>{saudacao}</span></h1>
              <p style="margin:0 0 16px 0;font-size:16px;line-height:1.6;color:#565b57;">{paragrafo1}</p>
              <p style="margin:0 0 34px 0;font-size:16px;line-height:1.6;color:#565b57;">{paragrafo2}</p>
              <div style="text-align:center;margin:0 0 30px 0;">
                <a href="{link}" style="display:inline-block;background:#2f7a56;color:white;font-size:16px;font-weight:600;text-decoration:none;padding:16px 40px;border-radius:12px;letter-spacing:0.01em;">{textoBotao}</a>
              </div>
              <div style="display:flex;align-items:center;gap:10px;justify-content:center;padding:14px 18px;background:#fbeae6;border-radius:12px;">
                <div style="width:8px;height:8px;border-radius:50%;background:#dd5a3a;flex-shrink:0;"></div>
                <p style="margin:0;font-size:14px;font-weight:600;color:#b8492e;">{aviso}</p>
              </div>
            </div>
          </div>

          <div style="padding:32px 40px 44px 40px;text-align:center;">
            <p style="margin:0 0 6px 0;font-size:13px;color:#8b8f8b;">Se você não esperava este e-mail, pode ignorá-lo com segurança.</p>
            <p style="margin:0;font-size:13px;color:#9a9d9a;">Clínica+Saúde — Sistema de Agendamento</p>
          </div>

        </div>
        </body></html>
        """;
}
