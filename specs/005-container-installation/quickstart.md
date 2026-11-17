# Validação da instalação Docker

## Preparação da instalação

No Windows x64, instale o Docker Desktop com WSL 2, inicie contêineres Linux e use Docker Engine 28 ou superior e PowerShell 7. Execute os comandos a partir da raiz do pacote, onde estão compose.yaml, .env.example e scripts/container. O computador precisa de acesso à internet e de permissão de leitura da imagem privada do GHCR. Autentique o Docker no GHCR com sua própria credencial antes da instalação.

Crie a configuração local a partir do modelo. Mantenha o arquivo fora do compartilhamento e restrinja seu acesso, pois ele contém senhas.

~~~powershell
Copy-Item .env.example .env
~~~

Preencha os campos do arquivo .env.

- COMPOSE_PROJECT_NAME identifica a instalação e seus volumes. Preserve o mesmo valor em reinicializações e atualizações.
- APP_IMAGE e SQL_IMAGE recebem as referências por digest da versão desejada. As referências do piloto estão abaixo.
- SQL_ADMIN_PASSWORD é a senha da conta administrativa sa do SQL Server. SQL_MIGRATION_USER e SQL_MIGRATION_PASSWORD identificam a conta que altera o esquema. SQL_APP_USER e SQL_APP_PASSWORD identificam a conta usada pela aplicação.
- SQL_MIGRATION_CONNECTION_STRING e SQL_APP_CONNECTION_STRING são conexões SQL completas que apontam para o serviço sql e o banco FinanceManager. Use as mesmas credenciais correspondentes aos campos anteriores e configure TrustServerCertificate=True para o SQL local. Use a sintaxe de escape de conexão SQL para valores com caracteres especiais.
- HTTPS_CERTIFICATE_PATH e DATA_PROTECTION_CERTIFICATE_PATH apontam para arquivos PFX acessíveis no Windows. Os campos PASSWORD correspondentes contêm as respectivas senhas. BACKUP_DIRECTORY aponta para o diretório local destinado aos backups.

Um exemplo de estrutura para a conexão de migração é Server=sql;Database=FinanceManager;User ID=USUARIO_MIGRACAO;Password=SENHA_MIGRACAO;Encrypt=True;TrustServerCertificate=True. A conexão da aplicação usa o respectivo usuário e senha. Substitua os marcadores por valores próprios.

A instalação precisa de um certificado HTTPS válido para localhost e confiável no perfil do usuário. Também precisa de um PFX com chave privada para proteger as chaves de autenticação. Para um piloto local, o mesmo PFX pode atender aos dois campos. O exemplo abaixo cria um certificado local e o confia no perfil atual. A senha solicitada será necessária nos campos de certificado do arquivo .env.

~~~powershell
$certDir = Join-Path $env:LOCALAPPDATA 'FinanceManager'
New-Item -ItemType Directory -Force -Path $certDir | Out-Null
$certPassword = Read-Host 'Senha do PFX' -AsSecureString
$cert = New-SelfSignedCertificate -DnsName localhost -CertStoreLocation Cert:\CurrentUser\My -KeyExportPolicy Exportable -KeySpec KeyExchange
Export-PfxCertificate -Cert $cert -FilePath (Join-Path $certDir 'localhost.pfx') -Password $certPassword
Export-Certificate -Cert $cert -FilePath (Join-Path $certDir 'localhost.cer')
Import-Certificate -FilePath (Join-Path $certDir 'localhost.cer') -CertStoreLocation Cert:\CurrentUser\Root
~~~

Guarde o PFX de proteção de chaves e sua senha junto ao procedimento de recuperação. O mesmo certificado precisa estar disponível quando forem reutilizadas as chaves persistidas.

## Instalação, parada e recuperação

Para instalar e abrir a aplicação:

~~~powershell
docker version --format '{{.Server.Version}}'
docker compose version
./scripts/container/Install.ps1
~~~

Acesse https://localhost:7043, crie a conta e entre. Para parar os serviços preservando os volumes:

~~~powershell
./scripts/container/Stop.ps1
~~~

Para atualizar, ajuste APP_IMAGE no arquivo .env para o digest da nova versão e execute o comando. O instalador obtém as imagens, para a API e cria um backup verificado antes da migração. Cada execução de atualização cria um backup, mesmo que não haja migração pendente. O caminho do arquivo é mostrado no resultado e um arquivo .json ao lado registra a versão anterior e o checksum. Mantenha o SQL_IMAGE igual ao da instalação existente.

~~~powershell
./scripts/container/Update.ps1
~~~

Para restaurar, use o arquivo .bak e mantenha o arquivo .bak.json correspondente no mesmo diretório. Consulte a imagem anterior nos metadados, obtenha-a no registro e configure APP_IMAGE no .env com essa referência. Mantenha os certificados e as senhas da instalação disponíveis.

~~~powershell
$backup = 'C:\caminho\para\o-backup.bak'
$metadata = Get-Content "$backup.json" -Raw | ConvertFrom-Json
docker pull $metadata.image
./scripts/container/Restore.ps1 -BackupPath $backup
~~~

A restauração deixa a API parada. Após verificar o resultado e o banco restaurado, inicie a versão anterior:

~~~powershell
./scripts/container/Install.ps1
~~~

## Matriz de validação

Para o piloto, use dados sintéticos, um projeto Compose exclusivo e duas versões de teste, com migração na segunda.

No navegador, abrir `https://localhost:7043`, criar conta, entrar e cadastrar um registro sintético. Validar o certificado e tentar acesso a partir de outro computador pela LAN. Confirmar acesso local e recusa externa. Conferir que a porta SQL não foi publicada.

| Caso | Ação | Resultado esperado |
| --- | --- | --- |
| Instalação limpa (SC-001, SC-004) | Executar o fluxo acima com volume SQL vazio e sem a imagem inicial no cache local, baixando-a do registro de teste | Banco e identidades provisionados, migrações aplicadas, HTTPS disponível e cadastro concluído. Reexecutar no mesmo volume e confirmar os dados preservados. Confirmar que a credencial da API opera e que o SQL rejeita uma alteração de esquema feita por ela |
| Recriação (SC-002) | Executar `./scripts/container/Stop.ps1`, remover contêineres do piloto com `docker compose down` sem remover volumes e executar `./scripts/container/Install.ps1` | Conta e registro continuam acessíveis |
| Atualização (SC-002) | Selecionar a segunda imagem por digest, baixá-la do registro de teste e executar `./scripts/container/Update.ps1` | Backup produzido, migração aplicada e registro preservado |
| Falha (FR-004 a FR-006) | Em instalação isolada, testar certificado inválido, SQL indisponível, falha de migração e Engine abaixo de 28. No caso do Engine incompatível, confirmar a mensagem específica em português e a interrupção antes de iniciar os serviços | Mensagem acionável, saída diferente de zero e nova versão indisponível |
| Recuperação (SC-003) | Em instalação isolada, selecionar versão compatível e executar `./scripts/container/Restore.ps1 -BackupPath <arquivo>`, depois iniciar a versão recuperada | Consulta retorna o registro salvo antes da atualização |

Executar os checks de build e testes de backend e Angular da CI. Antes do piloto, registrar as versões e os digests das duas imagens da aplicação e a referência fixada do SQL Server. Durante o piloto, registrar o resultado por caso e a evidência da restauração. Ao terminar, executar `./scripts/container/Stop.ps1` e confirmar que `docker compose ps --status running` não lista serviços do piloto.

## Imagens do piloto inicial

- Aplicação: `ghcr.io/carlosmancini/financemanager-pilot@sha256:d7e444b5f26c26289fd00f65b5b0aba1a0ece44a1e5608a41c0e9d1ffd96609d`.
- SQL Server: `mcr.microsoft.com/mssql/server@sha256:05644dad1266ca3d445d1e8f6d2ebb9a6a03df9a39376e0f7b48917356dca9d9`.

## Evidências parciais da instalação inicial

Em 2026-11-14, um projeto Compose isolado com dados sintéticos iniciou o SQL Server, aplicou as migrações e iniciou a API com a imagem do piloto. A porta HTTPS foi publicada em 127.0.0.1 e o SQL Server não teve porta publicada. Uma senha incorreta de certificado foi rejeitada antes da inicialização. Uma verificação com resposta simulada do Docker Engine 27 confirmou a mensagem de versão mínima.

O navegador integrado rejeitou o certificado de teste por não confiar na autoridade emissora. Permanecem pendentes a verificação de HTTPS com certificado confiável, o cadastro pela interface, a instalação sem camadas em cache e a tentativa a partir de outro computador na LAN. T010 permanece aberta.

## Evidência da preservação após recriação

Em 2026-11-14, um projeto Compose isolado foi instalado com dados sintéticos. Após cadastro, login e criação de uma conta financeira pela API, o comando de parada interrompeu os serviços. Os contêineres foram removidos sem remover os volumes e a instalação foi executada novamente. O login e a conta financeira continuaram disponíveis, os arquivos de chaves mantiveram os mesmos hashes e a quantidade de migrações aplicadas permaneceu igual. Os contêineres e volumes de teste foram removidos ao final.

## Imagem de teste da atualização

A segunda imagem do piloto, usada somente para testar migração, é ghcr.io/carlosmancini/financemanager-pilot@sha256:129a4d6165798d170598b10b11743c063b5a2f604f70e402e6a14d9e53576cce. Ela contém uma migração sintética de validação.

## Evidência de atualização e recuperação

Em 2026-11-14, uma instalação isolada com conta financeira sintética foi atualizada para a imagem de teste. O backup anterior foi criado fora do volume do banco e protegido por permissões de arquivo. A migração sintética foi aplicada e a conta continuou acessível. Uma migração com configuração inválida falhou e a API permaneceu parada. A restauração do backup com a imagem anterior recuperou a conta e o esquema anterior. Os testes também rejeitaram checksum incorreto, imagem anterior incompatível e troca da imagem SQL antes do backup. Os recursos e dados sintéticos foram removidos ao final.


## Checks de verificação final

Em 2026-11-14, a restauração de pacotes com lock file e o build Release da solução passaram. O NuGet retornou NU1900 porque não foi possível consultar os dados de vulnerabilidades. Passaram 98 testes unitários e 80 testes de integração com SQL Server isolado. O build Angular e 50 testes do frontend passaram em contêiner temporário.

A T018 permanece aberta enquanto a T010 estiver pendente. Os testes de cadastro pela interface com certificado confiável, instalação sem camadas em cache e recusa a partir de outro computador na LAN ainda precisam ser registrados.