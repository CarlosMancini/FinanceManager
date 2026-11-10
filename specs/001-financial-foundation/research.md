# Pesquisa técnica · 001 · Fundação financeira

## Stack e testes

**Decisão.** Fixar o SDK .NET em `10.0.403` no `global.json` e usar `net10.0` com C# 14. Adotar ASP.NET Core `10.0.14` e fixar Identity EF, EF Core SQL Server/Design, ferramenta `dotnet-ef` e `Microsoft.AspNetCore.Mvc.Testing` em `10.0.14`. Usar SQL Server 2025 Express LocalDB no Windows. Fixar Angular e Angular CLI em `22.2.5`, Node LTS em `24.22.2` e TypeScript em `6.0.3`. Usar xUnit.net v3 (`xunit.v3.mtp-v2` `4.0.3` com Microsoft Testing Platform) e `Microsoft.Extensions.TimeProvider.Testing` `10.11.0`. Registrar dependências e ferramentas de versão explícita, lockfiles NuGet e npm e restaurar em modo bloqueado.

**Motivo.** A combinação Angular, Node e TypeScript está dentro das faixas do compilador. LocalDB atende ao ambiente de um titular e permite validar migrações, collation, índice e `rowversion` no motor SQL Server. Os testes xUnit isolam serviços e regras com dublês de portas e relógio controlado. Os testes HTTP usam `WebApplicationFactory` e uma base SQL de teste separada, com isolamento entre cenários que gravam dados.

**Alternativas avaliadas.** Uma instância SQL Server em contêiner acrescentaria um pré-requisito ao ambiente Windows inicial. SQLite e EF InMemory não reproduzem as garantias do provedor exigidas pelos testes de integração. Testes de navegador complementam os testes unitários e de integração.

[.NET 10.0.14](https://github.com/dotnet/core/blob/main/release-notes/10.0/10.0.14/10.0.14.md) · [EF Core SQL Server](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.SqlServer/10.0.14) · [SQL Server LocalDB](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb) · [Angular e TypeScript](https://github.com/angular/angular/blob/v22.2.5/packages/compiler-cli/package.json) · [Angular CLI](https://github.com/angular/angular-cli/releases/tag/v22.2.5) · [Node 24.22.2](https://nodejs.org/en/blog/release/v24.22.2) · [xUnit v3](https://xunit.net/releases/v3/4.0.3) · [FakeTimeProvider](https://www.nuget.org/packages/Microsoft.Extensions.TimeProvider.Testing/10.11.0) · [Testes EF Core](https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy) · [Testes de integração ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0)

## Sessão de 30 minutos e revogação

**Decisão.** Usar Identity para hash, validação de credenciais e bloqueio de tentativas. O comando local provisiona o titular. A autenticação por cookie usa `ITicketStore` com sessão no SQL Server. Na leitura do ticket, derivar seu prazo de `LastActivityAtUtc + 30 min` e rejeitar no limite exato. Após autenticação, autorização de rota e antiforgery aplicável, atualizar a atividade por operação condicional, com relógio injetável e proteção contra retrocesso e revogação concorrente. A renovação de metadados do ticket preserva a última atividade e nunca recria uma sessão revogada. Logout revoga a sessão antes de limpar o cookie.

**Motivo.** A validade acompanha cada atividade admitida, inclusive quando há requisições concorrentes. A revogação no servidor invalida cópias do cookie.

**Alternativas avaliadas.** Expiração deslizante nativa renova após a metade da janela e não representa o prazo exato da última atividade. Um token autocontido exigiria mecanismo adicional de revogação para invalidar cópias após logout.

[Handler de cookie do ASP.NET Core 10](https://github.com/dotnet/aspnetcore/blob/v10.0.0/src/Security/Authentication/Cookies/src/CookieAuthenticationHandler.cs#L135-L205) · [Expiração deslizante](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.authentication.cookies.cookieauthenticationoptions.slidingexpiration?view=aspnetcore-10.0) · [Configuração do Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-10.0)

## Origem HTTPS e antiforgery

**Decisão.** Servir a SPA e a API na mesma origem HTTPS em loopback. Usar cookie de autenticação Secure e HttpOnly. O Angular envia o token antiforgery em `X-XSRF-TOKEN` nas mutações relativas à origem. O ASP.NET Core valida o token em cada mutação. Obter token novo após mudança de identidade e proteger a persistência das chaves do Data Protection.

**Motivo.** O desenho mantém credenciais fora do JavaScript e permite a validação de CSRF pelo servidor, inclusive em login e logout.

**Alternativas avaliadas.** Origens distintas exigiriam configuração adicional de CORS e cookies. SameSite sozinho não cumpre a exigência de validação antiforgery.

[Antiforgery no ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0) · [XSRF no Angular](https://angular.dev/best-practices/security#httpclient-xsrfcsrf-security) · [Data Protection](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/introduction?view=aspnetcore-10.0)

## Nomes de conta

**Decisão.** Gerar uma chave normalizada no domínio e usar índice único não filtrado em `(OwnerId, NormalizedName)`, com collation binária explícita na chave. O domínio remove espaços nas extremidades e ignora diferenças de caixa, preservando acentos. Em corrida de escrita, a infraestrutura reconhece a violação do índice específico e devolve o conflito sem expor a mensagem SQL.

**Motivo.** O comportamento é consistente com contas arquivadas, titulares distintos e cadastro ou renomeação simultâneos.

**Alternativas avaliadas.** Uma consulta prévia isolada admite corrida entre leitura e escrita. A collation padrão do banco pode aplicar equivalências diferentes das regras da feature.

[Comparação de strings em .NET](https://learn.microsoft.com/en-us/dotnet/standard/base-types/best-practices-strings) · [Collations no EF Core](https://learn.microsoft.com/en-us/ef/core/miscellaneous/collations-and-case-sensitivity) · [Índices no EF Core](https://learn.microsoft.com/en-us/ef/core/modeling/indexes)

## Edição concorrente

**Decisão.** Usar `rowversion` como revisão da conta e expor ETag forte. Exigir `If-Match` em edições e mudanças de estado. A aplicação verifica a revisão enviada e o EF Core protege a escrita contra corrida com a mesma revisão. Divergência devolve `412`, com mensagem em português em Problem Details.

**Motivo.** Preserva a alteração gravada enquanto o formulário antigo estava aberto. A verificação SQL protege o intervalo entre a leitura e a escrita.

**Alternativas avaliadas.** Última gravação venceria silenciosamente. Um contador mantido pela aplicação exigiria garantir seu incremento em todos os caminhos de escrita.

[Concorrência no EF Core](https://learn.microsoft.com/en-us/ef/core/saving/concurrency) · [If-Match](https://www.rfc-editor.org/rfc/rfc9110.html#section-13.1.1) · [Precondição obrigatória](https://www.rfc-editor.org/rfc/rfc6585.html#section-3) · [Problem Details](https://www.rfc-editor.org/rfc/rfc9457.html)
