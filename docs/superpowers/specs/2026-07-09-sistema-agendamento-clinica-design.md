# Sistema de Agendamento para Clínica Médica — Design (MVP)

## Contexto e Objetivo

Sistema para gerenciar o agendamento de consultas de uma clínica médica. O
agendamento é feito pela recepção da clínica (não é auto-agendamento pelo
paciente). Foco do MVP: agenda de consultas, sem prontuário/histórico
clínico.

Stack definida pelo usuário: back-end em C# .NET, banco PostgreSQL,
front-end em React + TypeScript. Arquitetura solicitada: MVC simples, com
segurança como prioridade transversal (ver `CLAUDE.md` do projeto).

## Escopo do MVP

**Dentro do escopo:**
- Cadastro de pacientes (dados básicos, sem prontuário).
- Cadastro de médicos, especialidades e horário de trabalho.
- Agendamento, cancelamento e reagendamento de consultas.
- Autenticação e autorização por papel (Admin, Recepção, Médico).
- Médico visualiza a própria agenda.

**Fora do escopo (fases futuras):**
- Auto-agendamento pelo paciente (login de paciente).
- Prontuário / histórico clínico.
- Notificações (e-mail/SMS de lembrete).
- Suporte a múltiplas clínicas (multi-tenant) — sistema é single-tenant.
- Bloqueios de agenda por feriado/férias do médico (pode ser adicionado
  depois via tabela de exceções).

## Papéis de Usuário

- **Admin**: gerencia médicos, especialidades, usuários e configurações da
  clínica.
- **Recepção**: cadastra pacientes e cria/cancela/reagenda consultas.
- **Médico**: visualiza a própria agenda (somente leitura das próprias
  consultas).

Não há login de paciente no MVP.

## Arquitetura

API monolítica em camadas simples (padrão MVC do ASP.NET Core), sem camada
de Repository redundante:

```
React + TypeScript (SPA)
        |  REST/JSON
        v
ASP.NET Core Web API
  Controllers (MVC)
        |
  Services (regras de negócio)
        |
  EF Core DbContext (Npgsql)
        |
   PostgreSQL
```

- `DbContext` do EF Core é usado diretamente pelos Services (já cumpre o
  papel de Repository/Unit of Work — camada extra seria abstração
  redundante para o tamanho deste projeto).
- Front-end React/TS é um projeto separado, consumindo a API via REST.
- Sistema single-tenant: uma instância atende uma clínica.

Alternativas consideradas e descartadas: Clean Architecture (overhead de
projetos/camadas desnecessário para o escopo e contraria o pedido de
arquitetura simples) e Vertical Slice Architecture (foge do padrão MVC
solicitado).

## Autenticação e Autorização

- **Access token JWT**, vida curta (15 min), mantido em memória no
  front-end (nunca em `localStorage`, evita roubo via XSS).
- **Refresh token**, vida mais longa (7 dias), em cookie `httpOnly` +
  `secure` + `SameSite=Strict`, usado só em `POST /api/auth/refresh`.
- Refresh tokens são persistidos como hash no banco (tabela
  `TokenRenovacao`), permitindo revogação (logout, troca de senha).
- Roles (Admin, Recepção, Médico) como claims no JWT, checadas via
  `[Authorize(Roles = "...")]` nos controllers.
- Toda regra de ownership (ex: médico só vê a própria agenda) é validada no
  Service contra o `UserId` da claim autenticada — nunca confiando em ID
  vindo da URL/body (proteção contra IDOR).
- Senhas via ASP.NET Core Identity (hash com algoritmo PBKDF2/BCrypt
  nativo do Identity).
- Rate limiting no endpoint de login; mensagens de erro genéricas
  ("credenciais inválidas") para não revelar se o e-mail existe
  (anti-enumeration).

## Modelo de Dados

Nomes de tabelas, colunas e classes C# em português.

- **Usuario**: `Id, Nome, Email, SenhaHash, Papel (Admin/Recepcao/Medico), Ativo, CriadoEm`
- **Medico**: `Id, UsuarioId (FK), EspecialidadeId (FK), CRM, DuracaoConsultaPadraoMinutos`
- **Especialidade**: `Id, Nome`
- **HorarioTrabalhoMedico**: `Id, MedicoId (FK), DiaSemana, HoraInicio, HoraFim`
- **Paciente**: `Id, Nome, CPF, Telefone, Email, DataNascimento, CriadoEm`
- **Consulta**: `Id, PacienteId (FK), MedicoId (FK), DataHora, DuracaoMinutos, Status (Agendada/Confirmada/Cancelada/Concluida/Faltou), Observacoes, CriadoPorUsuarioId (FK), CriadoEm, AtualizadoEm`
- **TokenRenovacao**: `Id, UsuarioId (FK), TokenHash, ExpiraEm, RevogadoEm, CriadoEm`

Disponibilidade dos médicos: horário fixo semanal (`HorarioTrabalhoMedico`)
+ duração padrão de consulta por médico. Os slots livres são calculados
subtraindo as `Consulta`s já marcadas do horário de trabalho do dia.

**Prevenção de double-booking:** validação no Service (checa sobreposição
antes de inserir) **+** constraint `EXCLUDE USING gist` no PostgreSQL
(extensão `btree_gist`) impedindo fisicamente dois agendamentos
sobrepostos para o mesmo médico, mesmo sob concorrência. Defesa em
profundidade: a checagem na aplicação dá mensagem amigável, a constraint
garante integridade mesmo em race condition.

CPF, telefone e e-mail do paciente são tratados como dado pessoal (LGPD):
sem exposição em logs, acesso restrito por role.

## Fluxo de Agendamento

1. Recepção seleciona médico + data → API calcula slots livres.
2. Recepção busca paciente existente (por CPF/nome) ou cadastra um novo.
3. Confirma horário → `POST /api/consultas` valida conflito e cria a
   `Consulta` com status `Agendada`.
4. Cancelamento/reagendamento: `PATCH /api/consultas/{id}` muda status ou
   `DataHora`, revalidando conflito.
5. Médico vê a própria agenda: `GET /api/consultas` filtrado pelo
   `MedicoId` derivado da claim do usuário autenticado, nunca por
   parâmetro livre na requisição.

## Tratamento de Erros

- Validação (ex: horário indisponível, CPF inválido): `400` com mensagem
  clara, sem detalhe interno.
- Erro inesperado: `500` genérico ao cliente; stack trace e detalhes só no
  log do servidor (Serilog).
- Conflito de agendamento pego pela constraint do banco: `409 Conflict`
  com mensagem "horário acabou de ser ocupado, escolha outro".

## Testes

- Unitários no Service layer: conflito de horário, geração de slots,
  regras de permissão/ownership.
- Integração na API para fluxos críticos: login, criar consulta, cancelar
  consulta.
- Sem E2E completo no MVP.

## Segurança (checklist aplicado)

- IDOR: toda consulta a recurso específico valida ownership contra a
  claim do usuário autenticado.
- Senhas via Identity (PBKDF2/BCrypt), nunca implementação própria.
- Rotas administrativas checam role no backend.
- Todo input externo tratado como não confiável; EF Core com queries
  parametrizadas (sem SQL concatenado).
- Sem hardcode de segredos; connection string e chave JWT via variáveis de
  ambiente / `.env` (nunca commitado; `.env.example` com placeholders).
- Cookies do refresh token: `httpOnly`, `secure`, `SameSite=Strict`.
- Rate limiting em login.
- Logs não guardam senha, token ou dado sensível em texto claro; eventos
  de login falho e mudança de permissão são logados para auditoria.
