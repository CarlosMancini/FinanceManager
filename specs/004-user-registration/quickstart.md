# Validação local · 004 · Cadastro e login

Executar após a implementação da 004. Este guia define validação futura, não evidencia testes já executados. Consultar [matriz do plano](plan.md#matriz-de-verificação), [modelo de dados](data-model.md) e contratos [HTTP](contracts/api.md) e [interface](contracts/ui.md).

## Pré-requisitos e preparação

Usar versões fixadas nos manifestos, SQL Server 2025 Express LocalDB (ou instância SQL Server de integração), PowerShell, certificado HTTPS confiável e dados exclusivamente sintéticos. Separar banco do navegador de banco dos testes; configurar conexões/segredos fora do repositório. Suíte de integração cria bases isoladas a partir de `ConnectionStrings__FinanceManagerTests` e exige permissão para criá-las/removê-las.

Na raiz, preparar uma base de navegador nova, sem titular. Não executar `provision-owner` no fluxo novo.

```powershell
$env:ConnectionStrings__FinanceManager = 'Server=(localdb)\MSSQLLocalDB;Database=FinanceManagerRegistrationLocal;Integrated Security=true;TrustServerCertificate=true'
$env:ConnectionStrings__FinanceManagerTests = 'Server=(localdb)\MSSQLLocalDB;Database=FinanceManagerTests;Integrated Security=true;TrustServerCertificate=true'
dotnet dev-certs https --trust
dotnet tool restore
dotnet restore .\FinanceManager.slnx --locked-mode
dotnet ef database update --project .\src\backend\FinanceManager.Infrastructure --startup-project .\src\backend\FinanceManager.Api
npm --prefix .\src\frontend ci
npm --prefix .\src\frontend run build
$env:Angular__BrowserRoot = (Resolve-Path .\src\frontend\dist\finance-manager\browser).Path
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --no-launch-profile --project .\src\backend\FinanceManager.Api -- --urls https://localhost:7043
```

## Testes e verificação de migração

Em outro terminal com a conexão de teste configurada:

```powershell
dotnet test --project .\tests\FinanceManager.UnitTests\FinanceManager.UnitTests.csproj --no-restore
dotnet test --project .\tests\FinanceManager.IntegrationTests\FinanceManager.IntegrationTests.csproj --no-restore
npm --prefix .\src\frontend test
dotnet ef migrations has-pending-model-changes --project .\src\backend\FinanceManager.Infrastructure --startup-project .\src\backend\FinanceManager.Api
```

Todos devem passar, incluindo regressões das features 001–003. Verificar migração completa em base vazia e atualização desde `20261111010901_AddBillsAndSettlements` em base sintética separada. A fixture de migração deve aplicar a versão anterior, preparar titular legado/hash e histórico de contas, movimentações, obrigações e baixas, aplicar migração 004 e então testar transição pela API. Não preparar legado pelo comando de produção retirado nesta feature nem manipular um banco pessoal.

Comparar antes/depois: Owner.Id, IDs e quantidades de todas as entidades financeiras, FKs, valores, origens e vínculos de baixa. Conferir que a migração remove `EmailIndex` antes de ampliar `NormalizedEmail` para nvarchar(max), mantendo os índices de nome interno e slot; verificar cadastro/login com e-mail válido maior que o limite antigo de 256 caracteres. Antes de concluir, perfil permanece incompleto e cookies antigos não acessam finanças. Depois de concluir, todos os dados financeiros são idênticos e login por e-mail funciona após reinício. Testar base inconsistente com mais de um titular: migração deve abortar sem excluir dados.

## Navegador e API

1. Abrir `https://localhost:7043`: login inicial com Criar conta. Sem sessão, leituras e mutações de contas, movimentações e obrigações não concedem acesso. Obter CSRF antes das mutações anônimas; omiti-lo deve resultar em 403.
2. Criar conta com dados sintéticos do contrato. Testar obrigatórios vazios/espaços, apelido ausente, nomes acentuados/apóstrofos, nascimento hoje/futuro/inválido, cada e-mail inválido e telefone com 9/10/15/16 dígitos. Verificar validação por campo em português e correção sem repetir dados não secretos. API rejeita as mesmas entradas.
3. Testar senha com 11/12/128/129 pontos de código, cada categoria ausente e caracteres suplementares Unicode. Espaços contam no comprimento, não como símbolo, e nunca são aparados. Resultado deve coincidir entre domínio, API, Identity e formulário.
4. Após cadastro, confirmar ausência de sessão financeira e retorno ao login sem senha preenchida. Entrar com e-mail variando caixa/trim e senha exata; sem contas, abrir cadastro de conta. Criar conta/movimentação/obrigação e confirmar navegação posterior ao histórico. Somente contas arquivadas não habilitam lançamento novo.
5. Na base legada preparada pela fixture, testar ambos os caminhos de prova: Usar acesso antigo no login e Criar conta → Completar cadastro existente. Prova inválida não altera nada; válida concede somente cookie restrito. Esse cookie não acessa qualquer API financeira. Completar dados e confirmar retorno ao login, mesmo titular/histórico e rejeição do nome antigo como login.
6. Com relógio controlado nos testes, provar credenciais e concluir imediatamente antes de dez minutos; repetir em cenários separados exatamente no limite e depois. Apenas o primeiro é aceito. Capturar VerifiedAtUtc no sucesso da comprovação e simular atraso de dois minutos aguardando bloqueio antes da emissão: restam oito minutos, não dez. Simular também atraso que complete dez minutos antes da emissão: exigir nova prova sem entregar cookie utilizável. Atividade não renova. Após expiração, pedir credenciais antigas de novo, mantendo dados não secretos preenchidos. Navegador deve reproduzir o fluxo sem depender de esperar dez minutos manualmente.
7. Simular resposta perdida e repetir cadastro/conclusão. Disparar cadastros/conclusões sincronizados em paralelo e uma prova concorrente à conclusão. Verificar um titular, um sucesso de conclusão e ausência de sobrescrita, duplicação de auditoria ou autorização utilizável após conclusão. Injetar falha entre as gravações para verificar rollback de perfil/hash, sessões, autorizações e auditoria.
8. Reutilizar cookies copiados antes da transição: sessão antiga e autorização de outra aba não funcionam após commit. Testar logout e 30 minutos de inatividade. Cinco senhas erradas provocam bloqueio de cinco minutos; limitador compartilhado impede alternar login/prova para exceder dez pedidos/minuto por IP. Verificar mensagens genéricas e titularidade na API.
9. Inspecionar logs/respostas/armazenamento do navegador: nenhuma senha, token de transição, perfil ou dado financeiro nos logs; cookie restrito Secure/HttpOnly/SameSite=Strict, sessão com atributos existentes; dados do formulário somente em memória. Verificar teclado, foco de erro, labels, leitura de status e tela estreita.

## Evidência de aceite

Registrar por FR/SC: comandos e resultados, cenários SQL/HTTP, capturas de interface sem segredos e comparação de histórico. Para SC-001, usar cinco participantes com dados sintéticos e instalação vazia por participante; pelo menos quatro cadastram e entram em até cinco minutos sem assistência. Teste automatizado não substitui essa medição. A aprovação do plano não marca esses critérios como cumpridos.
