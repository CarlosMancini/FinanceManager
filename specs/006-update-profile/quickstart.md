# Quickstart de validação — 006 · Atualização cadastral

Revisão de 2026-11-22 após esclarecimento B de FR-010/SC-005. Este guia será executado após implementação e tarefas revisadas para changes/originalValues e aprovadas. O tasks.md anterior ainda precisa de atualização por `$speckit-tasks`. Não constitui evidência de testes já realizados. Consultar a [matriz do plano](plan.md#matriz-de-verificação), os contratos [HTTP](contracts/api.md) e [UI](contracts/ui.md) e o [modelo](data-model.md).

## Preparação

Na raiz do repositório, usar SDK/Node e dependências fixadas nos manifestos. Exigir SQL Server real, certificado HTTPS local já confiável e duas bases descartáveis distintas: navegador e testes. Usar somente dados sintéticos. A suíte SQL exige ConnectionStrings__FinanceManagerTests com permissão de criar/remover bancos isolados; credenciais reais ficam fora do repositório. Não apontar para banco pessoal do usuário.

```powershell
Set-Location C:/Projetos/FinanceManager
$env:ConnectionStrings__FinanceManager = 'Server=(localdb)\MSSQLLocalDB;Database=FinanceManagerProfileLocal;Integrated Security=true;TrustServerCertificate=true'
$env:ConnectionStrings__FinanceManagerTests = 'Server=(localdb)\MSSQLLocalDB;Database=FinanceManagerTests;Integrated Security=true;TrustServerCertificate=true'
dotnet tool restore
dotnet restore ./FinanceManager.slnx --locked-mode
npm --prefix ./src/frontend ci
```

Se o certificado ainda não for confiável, configurar o certificado local antes da navegação. Registrar comando/resultado e seguir as aprovações do ambiente; nunca contornar HTTPS ou expor serviço além de loopback.

## Build e testes finitos

```powershell
dotnet build ./FinanceManager.slnx --no-restore --disable-build-servers
npm --prefix ./src/frontend run build
dotnet test --project ./tests/FinanceManager.UnitTests/FinanceManager.UnitTests.csproj --no-restore --filter-class '*OwnerProfile*'
dotnet test --project ./tests/FinanceManager.IntegrationTests/FinanceManager.IntegrationTests.csproj --no-restore --filter-class '*OwnerProfile*'
npm --prefix ./src/frontend test -- --include='src/app/auth/owner-profile*.spec.ts'
```

Padronizar novas classes relevantes com OwnerProfile no nome para esses filtros abrangerem domínio, aplicação, contrato, persistência, concorrência e migração. Zero testes encontrados não conta como sucesso; usar os nomes definidos em tasks se houver ajuste aprovado. O filtro de classes é o do [xUnit/Microsoft.Testing.Platform](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform), conforme runner do global.json, sem usar expressão VSTest.

Executar também regressão direcionada do módulo de autenticação, porque validações e sessão são compartilhadas:

```powershell
dotnet test --project ./tests/FinanceManager.UnitTests/FinanceManager.UnitTests.csproj --no-restore --filter-class '*Authentication*'
dotnet test --project ./tests/FinanceManager.IntegrationTests/FinanceManager.IntegrationTests.csproj --no-restore --filter-class '*Authentication*'
npm --prefix ./src/frontend test -- --include='src/app/auth/*.spec.ts'
```

Os filtros de Authentication cobrem classes qualificadas do namespace existente; confirmar a contagem e ausência de skips indevidos. Testes de isolamento financeiro que estejam em Accounts/Transactions/Bills devem ser executados de forma direcionada conforme tarefas de regressão; ampliar para suíte completa somente se alterações ou falhas justificarem. Registrar resultados e limitações de infraestrutura, sem marcar testes não executados como aprovados.

## Persistência e migração

```powershell
dotnet ef migrations has-pending-model-changes --project ./src/backend/FinanceManager.Infrastructure --startup-project ./src/backend/FinanceManager.Api
dotnet ef database update --project ./src/backend/FinanceManager.Infrastructure --startup-project ./src/backend/FinanceManager.Api
```

A integração deve testar instalação vazia e upgrade da migração atual `20261116185815_AllowMultipleOwners` para a nova `AllowProfileUpdateAudit`. Preparar previamente titulares, hash, sessões, auditorias e dados financeiros sintéticos; comparar IDs, FKs, contagens e valores depois. Confirmar que ProfileUpdated é aceito, valores de ação desconhecidos continuam rejeitados e rollback preserva tudo. Testar downgrade bloqueado quando existem eventos novos, sem remover eventos para forçar êxito. Não executar downgrade em base de uso.

## Execução temporária para navegador e API

Executar somente quando builds/testes estáticos não substituírem a inspeção do fluxo. Usar a SPA compilada servida pelo backend; nenhum watcher é necessário.

```powershell
$env:Angular__BrowserRoot = (Resolve-Path ./src/frontend/dist/finance-manager/browser).Path
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$profileValidationHost = Start-Process -FilePath dotnet -ArgumentList @('run', '--no-build', '--no-launch-profile', '--project', './src/backend/FinanceManager.Api', '--', '--urls', 'https://localhost:7043') -WindowStyle Hidden -PassThru
$profileValidationHost | Select-Object Id, StartTime
```

Registrar PID, horário de início e PIDs dos filhos do host, exclusivamente para esta validação. Aguardar prontidão com tentativas limitadas antes de abrir `https://localhost:7043` no navegador integrado `@Browser`. Se indisponível, relatar a limitação; não abrir navegador externo sem autorização explícita. Não registrar senha/corpos pessoais nas evidências.

## Roteiro observável

1. Criar titulares sintéticos A e B por register, autenticando em sessões independentes. A sem conta financeira deve ver a opção “Atualizar Cadastro”. B pode ter histórico sintético. Após login, selecionar a opção uma vez e conferir os cinco campos preenchidos, opção de senha e botão no final. Sem sessão, rota direta exige login. (SC-001/005)
2. Em A, mudar somente telefone, enviar, reabrir e comparar nome/apelido/nascimento/e-mail e credencial anteriores. Apagar apelido explicitamente e verificar remoção; deixar intacto e verificar preservação. Editar/reverter campo, mudar apenas caixa/trim do e-mail e enviar sem outras mudanças: mensagem de ausência de alterações, hash/auditoria intactos. (SC-002)
3. Digitar nome vazio, data futura, e-mail sem extensão e telefone incompleto; sair dos campos e abrir senha. Nenhum erro novo nem requisição de validação deve ocorrer antes do submit. Ao enviar, erros por campo impedem gravação e preservam dados pessoais preenchidos. Verificar máscaras, limite de 11 dígitos e ausência de indicadores dinâmicos. (SC-003)
4. Acionar “Alterar senha”: exatamente três campos vazios. Sem acioná-la, atualização pessoal não exige senha. Com seção aberta, verificar atual ausente/incorreta, senha nova 11/12/128/129 caracteres Unicode, categorias ausentes, confirmação divergente e valores com espaços. Testar prova válida de senha antiga fora da nova política em fixture preparada. (SC-003/004)
5. Enviar senha atual correta, nova válida e confirmação idêntica, com mudança pessoal simultânea. Confirmar nova senha e perfil depois de novo login; a senha anterior distinta é rejeitada. Mudar e-mail, sair e entrar com o novo; antigo e-mail falha. O OwnerId e todo histórico continuam iguais. Verificar que sessão de outra aba já autenticada permanece pelas regras existentes. (SC-002/004)
6. Pela integração/API, simular falha após preparar perfil/hash e antes do commit. Nenhum dado, hash ou evento pode mudar. Gerar cinco comprovações incorretas: proteção persiste apesar da rejeição e bloqueia por cinco minutos; senha correta durante bloqueio também não troca dados. Verificar dez pedidos/minuto por IP. Usar relógio de testes para expiração em vez de esperas longas. (SC-003/005)
7. Abrir A em duas sessões com o mesmo snapshot; alterar telefone em uma e nome na outra, enviando changes/originalValues correspondentes. Ambas as diferenças devem permanecer. Em seguida, editar o mesmo campo nas duas abas: primeiro envio válido confirma, segundo retorna 409 sem gravar nenhum dado, senha combinada ou evento. Repetir quando o valor pretendido da segunda já coincidir com o vigente: deve continuar em conflito. Conferir normalizações de apelido null/vazio, telefone/máscara, e-mail caixa/Unicode e nome com trim. No conflito, manter rascunho/snapshot, consultar atuais separadamente e só rebasear após revisão explícita; novo submit é sempre manual. Falha no GET de revisão não pode renovar a referência nem perder o rascunho. Simular resposta perdida depois do commit e repetir o PATCH antigo: deve retornar conflito por original obsoleto, sem deduzir sucesso ou reaplicar senha. Após revisão, o envio sem delta usa ambos os objetos vazios e retorna unchanged. Disputar troca de senha: credencial anterior não pode vencer depois da primeira troca. Com A e B, disputar e-mail disponível: no máximo um sucesso, outro com 409 e sem alteração parcial. Incluir corrida com register. (SC-002/005)
8. Com cliente HTTP autenticado, obter CSRF pelo fluxo existente e exercitar GET/PATCH dos contratos. Testar changes/originalValues com mesmas chaves, ambos vazios na troca isolada, originalValues ausente/null, original faltante ou excedente, campo desconhecido/duplicado, tipos inválidos, campos omitidos/null e diferença canônica nos originais, ID de terceiro no corpo/URL, JSON inválido, CSRF ausente, e-mail de B, sessão revogada/expirada, falha e conflito. Não há consulta/escrita do titular B pela sessão de A. Senha atual errada é 400 por campo; sessão ausente é 401. (SC-003/005)
9. Inspecionar respostas/logs/armazenamento: GET e PATCH no-store; senhas/hash/stamps não aparecem no retorno; nenhum dado pessoal/segredo no log; formulário só em memória; campos secretos descartados no sucesso/abandono/expiração. Simular falha de GET: não liberar formulário vazio para envio. Verificar labels, foco no erro, status, teclado e tela estreita. (SC-001–005)
10. Para SC-006, cinco participantes com dados sintéticos devem localizar e concluir uma atualização, incluindo troca opcional de senha. Pelo menos quatro concluem em até cinco minutos sem assistência. Registrar tempos e resultado sem dados pessoais. Não declarar esse critério atendido apenas com testes automatizados.

## Limpeza obrigatória e evidências

A limpeza deve ocorrer em finally na execução automatizada, inclusive em falha/interrupção. Guardar o objeto e a árvore de PIDs antes da inspeção; encerrar somente o host criado e seus filhos. Por exemplo, enquanto o PID ainda corresponde ao processo registrado:

```powershell
if (-not $profileValidationHost.HasExited) {
    taskkill /PID $profileValidationHost.Id /T /F
    $profileValidationHost.WaitForExit(10000) | Out-Null
}
Get-Process -Id $profileValidationHost.Id -ErrorAction SilentlyContinue
```

Confirmar também que todos os filhos registrados terminaram; qualquer processo remanescente precisa ser encerrado e a verificação repetida. Não matar processos de outras sessões, nem todos os dotnet. Se compilação iniciar servidores exclusivos desta tarefa e não houver sessão alheia afetada, executar `dotnet build-server shutdown`; caso contrário, isolar build com servidores desativados e limpar somente os próprios processos.

Encerrar o host antes de informar conclusão e não iniciar aplicativos após essa limpeza. Registrar FR/SC, comandos, contagens e resultados, capturas sem segredos, comparações SQL, migração, avaliação humana e PIDs encerrados. Revisar `git diff --check`, diff final e `git status --short`; alterações não relacionadas preexistentes devem ser preservadas. Sem commit/push nesta fase sem pedido explícito.
