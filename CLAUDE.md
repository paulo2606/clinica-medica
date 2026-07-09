# Diretrizes de Segurança do Projeto

Estas regras se aplicam a TODO código escrito ou editado neste projeto,
independente da tarefa pedida. Escreva pensando em segurança por padrão
(secure by design), não como uma etapa separada no final.

## Autenticação e Autorização
- Toda rota/endpoint que acessa dado de um usuário específico DEVE verificar
  que o usuário autenticado é o dono do recurso (evitar IDOR). Nunca confie
  apenas em um ID vindo da URL/body sem checar ownership contra a sessão.
- Nunca implemente sua própria criptografia de senha — use bcrypt/argon2 já
  estabelecidos na stack do projeto.
- Toda rota administrativa precisa checar explicitamente o papel/role do
  usuário no backend, nunca apenas esconder o botão no frontend.

## Validação e Sanitização de Input
- Trate todo input externo (body, query params, headers, cookies, arquivos)
  como não confiável, mesmo que o frontend já valide.
- Use queries parametrizadas/ORM — nunca concatene strings para montar SQL.
- Ao renderizar dado de usuário em HTML, sempre escape/sanitize (evitar XSS).
  Nunca use `dangerouslySetInnerHTML` (ou equivalente) com dado não sanitizado.
- Valide tipo, tamanho e formato de arquivos enviados; nunca confie na
  extensão ou no `Content-Type` declarado pelo cliente.

## Segredos e Configuração
- Nunca hardcode API keys, senhas, tokens ou strings de conexão no código.
  Use variáveis de ambiente (`.env`, secret manager).
- Nunca commite arquivos `.env` reais — apenas `.env.example` com placeholders.
- Mensagens de erro para o cliente não devem vazar stack trace, versão de
  framework, ou detalhes internos. Log detalhado fica só no servidor.

## Sessão e Tokens
- Cookies de sessão: `httpOnly`, `secure`, `SameSite=Strict` (ou `Lax` quando
  necessário para o fluxo).
- Tokens (JWT ou similares) devem ter expiração curta e mecanismo de revogação
  quando fizer sentido para o fluxo.

## Rate Limiting e Abuso
- Endpoints sensíveis (login, reset de senha, criação de conta, pagamento)
  precisam de rate limiting.
- Considere enumeration attacks: mensagens de erro de login não devem revelar
  se o e-mail existe ou não.

## Dependências
- Ao adicionar uma nova dependência, prefira pacotes mantidos ativamente.
  Se notar uma dependência com CVE conhecido durante o trabalho, sinalize.

## Logging
- Nunca logue senhas, tokens, números de cartão ou outros dados sensíveis em
  texto claro.
- Eventos de segurança relevantes (login falho, mudança de permissão,
  exclusão de dados) devem ser logados com contexto suficiente para auditoria.

## Quando tiver dúvida
Se uma implementação envolver trade-off de segurança que não está claro
(ex: nível de rate limit, política de expiração de token), pergunte antes de
assumir — não escolha silenciosamente a opção menos segura por conveniência.

---

Para uma auditoria completa e sob demanda de vulnerabilidades, use a skill
`security-audit`.
