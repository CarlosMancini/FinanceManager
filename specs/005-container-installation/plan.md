# Plano de implementação da instalação em contêineres

**Branch** `codex/conteinerizacao` · **Data** 2026-11-14 · [Spec 005](spec.md)

## Resumo

Empacotar Angular e ASP.NET Core na mesma imagem e executar a aplicação e o SQL Server com Docker Compose. A aplicação atenderá em HTTPS no loopback do Windows. Cada instalação terá dados e chaves próprios.

## Contexto técnico

- Angular 22, Node 24 no build, ASP.NET Core e EF Core em .NET 10, SQL Server 2025 e scripts PowerShell. Usar os manifestos existentes e fixar as imagens usadas na validação e na distribuição.
- Windows x64 com Docker Desktop e WSL 2, Docker Engine 28 ou superior, contêineres Linux amd64, um titular por instalação. Não há meta nova de desempenho.
- Build multiestágio com `npm ci` e restauração NuGet travada. Copiar o Angular compilado para o caminho indicado por `Angular__BrowserRoot`. Rodar a API com usuário sem privilégios.
- Declarar modo explícito de contêiner no Kestrel. Nesse modo, escutar IPv4 na porta interna 7043 com certificado montado e manter a validação do host localhost. Publicar somente `127.0.0.1:7043:7043`. Manter a escuta nativa em loopback fora desse modo. Banco e etapa de migração ficam na rede privada, sem portas publicadas.
- Usar volumes nomeados para SQL Server e Data Protection. Proteger as chaves em repouso com certificado montado e restringir acesso a volumes, segredos e backups.
- O Compose de instalação aceitará imagens versionadas de registro. Usar build local para desenvolvimento e registro de teste para validar o download. Publicar manualmente no registro de teste a imagem inicial e a imagem da segunda versão, que contém uma migração de teste. Registrar os digests e a referência fixada do SQL Server no quickstart. O workflow de publicação pertence à etapa posterior.

## Verificação constitucional

Antes e depois do desenho, a stack, as camadas, a titularidade e os fluxos financeiros permanecem de acordo com a constituição. HTTPS, cookies seguros e CSRF continuam aplicados. A porta publicada fica restrita ao loopback do Windows. A API opera com credenciais SQL limitadas e a migração usa credencial própria. Os testes usam dados sintéticos, xUnit para regressões do backend e SQL Server real para migração e restauração. A conformidade operacional depende da execução do [quickstart](quickstart.md).

## Estrutura prevista

- Raiz do repositório com `Dockerfile`, `.dockerignore`, `compose.yaml` e modelo de configuração.
- `src/backend/FinanceManager.Api/` para configuração HTTPS e Data Protection.
- `src/frontend/` para consumir o build Angular existente.
- `scripts/container/` para os comandos de instalação, atualização, parada e recuperação.
- `tests/FinanceManager.IntegrationTests/` para validar a configuração e as regressões pertinentes.

## Sequência de implementação e validação

1. Build da imagem e serviços Compose com persistência e HTTPS (FR-001 a FR-004).
2. Após o SQL ficar saudável, usar a credencial administrativa apenas no provisionamento inicial do banco, das identidades e das permissões. O provisionamento pode ser repetido sem recriar o banco ou apagar dados. Executar a migração com a identidade autorizada a alterar o esquema. A API recebe apenas as permissões necessárias à operação (FR-004, FR-005).
3. Atualização, backup e restauração, com execução exclusiva por instalação (FR-006).
4. Download da imagem identificada e documentação de operação (FR-007 e FR-008).
5. Executar a [matriz de validação](quickstart.md) e revisar as evidências de SC-001 a SC-004.

[Pesquisa](research.md) · [Estado persistente](data-model.md) · [Contrato operacional](contracts/installation.md)
