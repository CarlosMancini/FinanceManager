# Validação local · 001 · Fundação financeira

Use a [matriz de versões](research.md#stack-e-testes) e SQL Server 2025 Express LocalDB com dados sintéticos. Separe o banco de teste do banco usado no navegador. Configure as conexões por variáveis de ambiente na sessão do terminal. Use autenticação integrada ao Windows quando disponível.

## Preparar e iniciar

Na raiz do repositório, em PowerShell, configure as duas bases locais com autenticação integrada e execute a preparação.

```powershell
$env:ConnectionStrings__FinanceManager = 'Server=(localdb)\MSSQLLocalDB;Database=FinanceManagerLocal;Integrated Security=true;TrustServerCertificate=true'
$env:ConnectionStrings__FinanceManagerTests = 'Server=(localdb)\MSSQLLocalDB;Database=FinanceManagerTests;Integrated Security=true;TrustServerCertificate=true'
dotnet dev-certs https --trust
dotnet tool restore
dotnet restore .\FinanceManager.slnx --locked-mode
dotnet ef database update --project .\src\backend\FinanceManager.Infrastructure --startup-project .\src\backend\FinanceManager.Api
dotnet run --project .\src\backend\FinanceManager.Api -- provision-owner
npm --prefix .\src\frontend ci
npm --prefix .\src\frontend run build
$env:Angular__BrowserRoot = (Resolve-Path .\src\frontend\dist\finance-manager\browser).Path
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --no-launch-profile --project .\src\backend\FinanceManager.Api -- --urls https://localhost:7043
```

O provisionamento pede o usuário e a senha em um terminal interativo e rejeita um segundo titular. Use uma senha com pelo menos 12 caracteres, incluindo maiúscula, minúscula, número e símbolo. A API serve o build Angular em `https://localhost:7043` e usa o diretório indicado em `Angular__BrowserRoot`. Acesse o endereço pelo navegador e confirme que a aplicação responde só em loopback.

## Executar testes

```powershell
dotnet test --project .\tests\FinanceManager.UnitTests\FinanceManager.UnitTests.csproj --no-restore
dotnet test --project .\tests\FinanceManager.IntegrationTests\FinanceManager.IntegrationTests.csproj --no-restore
```

Os testes unitários cobrem a [matriz do plano](plan.md#matriz-de-verificação), com relógio controlado para os limites de sessão. A suíte de integração aplica as migrações em SQL Server e verifica as respostas do [contrato HTTP](contracts/api.md), incluindo duas gravações concorrentes e reuso do cookie após logout. Os dois comandos devem terminar com sucesso.

## Validar pela interface e pela API

1. Antes do login, peça `GET /api/accounts`. Espere `401`. Entre com o titular provisionado, confira a lista vazia e saia. A mesma leitura volta a retornar `401`. Na aba de rede, confira HTTPS, o cookie seguro e o cabeçalho CSRF nas mutações.
2. Entre novamente, cadastre `Carteira` do tipo carteira e verifique BRL. Tente criar ` carteira ` com outro tipo e espere erro de nome duplicado. Arquive a conta, confirme que segue consultável e repita a tentativa. O nome deve continuar reservado. Reative a conta.
3. Abra a edição em duas abas. Salve um novo nome na primeira. Tente salvar a segunda usando os dados anteriores. Espere `412` na rede, orientação para recarregar na tela e o novo nome preservado. Recarregue antes de tentar editar novamente.
4. Tente salvar um nome em branco. Confirme a mensagem em português. Reinicie a API sem recriar o banco, entre se solicitado e confira nome, tipo e estado persistidos.
5. Valide a sessão com o relógio controlado da suíte de integração. Uma requisição admitida antes de 30 minutos reinicia o prazo. Ao completar 30 minutos sem atividade, espere `401` e novo login. Uma mutação bloqueada por CSRF deve deixar o prazo intacto.
