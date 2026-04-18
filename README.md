# 🔐 Sistema de Autenticação com Login + JWT

_API RESTful em ASP.NET Core para cadastro, login, JWT, refresh token, perfil protegido e recursos extras de autenticação._

[![CI - GitHub Actions](https://img.shields.io/badge/CI-GitHub_Actions-2088FF?logo=githubactions&logoColor=white)](./.github/workflows/ci.yml)

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)
![ASP.NET Core Web API](https://img.shields.io/badge/ASP.NET%20Core-Web%20API-5C2D91)
![EF Core SQLite](https://img.shields.io/badge/EF%20Core-SQLite-0F6CBD)
![JWT Bearer](https://img.shields.io/badge/JWT-Bearer-000000)
![Status estudo](https://img.shields.io/badge/status-estudo-success)

---

O **Sistema de Autenticação com Login + JWT** é um projeto de estudo com foco em boas práticas de API: controllers organizados, services, DTOs, Identity, JWT, refresh token, confirmação de email e recuperação de senha.

## 🏷️ CI e Coverage

- O pipeline de CI executa restore, build, testes e upload de coverage.
- O coverage é gerado com `XPlat Code Coverage` e enviado para o Codecov no GitHub Actions.
- Depois de publicar o repositório no GitHub, você pode adicionar o badge de coverage apontando para o slug final do projeto.

## ✨ Funcionalidades

- Cadastro de usuário com hash de senha
- Login com geração de JWT
- Refresh token com persistência em banco
- Rota protegida para visualizar o próprio perfil
- Atualização de perfil e troca de senha
- Roles `Admin` e `User`
- Rota exclusiva para `Admin`
- Confirmação de email por token
- Recuperação de senha por token enviado por email
- Bloqueio após várias tentativas de login
- Logging das tentativas de autenticação

## 🧱 Estrutura do projeto

- **Controllers**: endpoints HTTP de autenticação, usuário e admin
- **Data**: `AppDbContext` e inicialização do banco
- **DTOs**: contratos de entrada e saída da API
- **Models**: usuário customizado, JWT, refresh token e email settings
- **Services**: geração de JWT, refresh token e envio de email
- **AuthApi.Tests**: testes de integração com xUnit

## 🛠️ Stack tecnológica

- C#
- .NET 10
- ASP.NET Core Web API
- ASP.NET Identity
- Entity Framework Core + SQLite
- JWT Bearer Authentication
- xUnit

## 📦 Endpoints

| Método | Rota | Descrição |
|---|---|---|
| POST | `/api/auth/register` | Cria um usuário |
| POST | `/api/auth/login` | Faz login e retorna JWT + refresh token |
| POST | `/api/auth/refresh` | Renova o access token |
| POST | `/api/auth/confirm-email` | Confirma o email do usuário |
| POST | `/api/auth/forgot-password` | Gera token de recuperação de senha |
| POST | `/api/auth/reset-password` | Redefine a senha |
| POST | `/api/auth/resend-confirmation` | Reenvia confirmação de email |
| GET | `/api/users/me` | Retorna o perfil do usuário autenticado |
| PUT | `/api/users/me` | Atualiza perfil |
| PUT | `/api/users/change-password` | Altera senha |
| GET | `/api/admin/dashboard` | Rota exclusiva para Admin |

### Exemplo de login

**POST** `/api/auth/login`

```json
{
	"email": "seuemail@gmail.com",
	"password": "Senha@1234"
}
```

### Exemplo de resposta

```json
{
	"accessToken": "eyJhbGciOiJIUzI1NiIs...",
	"accessTokenExpiresAtUtc": "2026-04-18T20:15:00Z",
	"refreshToken": "b1Qk...",
	"refreshTokenExpiresAtUtc": "2026-04-25T20:15:00Z"
}
```

## 🚀 Como executar

```powershell
cd C:\Users\gabriel\RiderProjects\Sistema de Autenticação (Login + JWT)\AuthApi
dotnet run
```

O projeto usa SQLite local com o arquivo `auth.db`.

## ⚙️ Configuração

Defina as variáveis de ambiente antes de executar a API:

- `JwtSettings__Key`
- `EmailSettings__UserName`
- `EmailSettings__Password`
- `EmailSettings__FromEmail`

Exemplo para PowerShell:

```powershell
$env:JwtSettings__Key = "uma_chave_longa_e_segura"
$env:EmailSettings__UserName = "seuemail@gmail.com"
$env:EmailSettings__Password = "sua_app_password_do_google"
$env:EmailSettings__FromEmail = "seuemail@gmail.com"
```

O arquivo `.env` da raiz do projeto serve como referência local.

## 🔍 Swagger / OpenAPI

Em ambiente de desenvolvimento, a documentação interativa fica disponível automaticamente quando a API é executada.

## 🧪 Testes

A suíte de testes de integração está em `AuthApi.Tests`.

Resultado validado:

- 4 testes executados
- 4 testes aprovados
- 0 falhas

Para rodar os testes:

```powershell
cd C:\Users\gabriel\RiderProjects\Sistema de Autenticação (Login + JWT)\AuthApi.Tests
dotnet test
```

## 🧯 Observações

- Segredos não devem ficar no repositório; use variáveis de ambiente ou secrets da IDE.
- A senha do Gmail precisa ser uma app password, não a senha normal da conta.
- Em produção, vale trocar SQLite por um banco gerenciado e revisar CORS, HTTPS e logging.

## 🤝 Contribuição

Sugestões de melhoria são bem-vindas. Priorize mudanças pequenas, objetivas e com documentação atualizada.
