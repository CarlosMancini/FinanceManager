# Decisões técnicas

| Decisão | Motivo | Alternativas avaliadas |
| --- | --- | --- |
| Uma imagem para Angular e API, com SQL Server separado no Compose | Reutiliza o serviço estático existente e mantém a mesma origem HTTPS | Servidor web separado adicionaria roteamento e configuração de origem |
| Windows x64 com WSL 2, contêineres Linux amd64 e SQL Server 2025 Express | Mantém SQL Server e um banco local por instalação. A imagem oficial requer x86-64 para o cenário suportado | LocalDB depende do Windows e foi excluído da instalação nova |
| Kestrel com HTTPS e escuta interna configurável, porta publicada no loopback | O loopback do contêiner é isolado do host. Publicar apenas em 127.0.0.1 preserva o acesso local. Usar Engine 28 ou superior e verificar acesso pela LAN no piloto | Proxy TLS adicionaria serviço e configuração de cabeçalhos encaminhados |
| Bundle EF Core com execução única após prontidão do SQL | Pode rodar sem SDK no computador e bloquear a inicialização da nova versão em caso de falha | Migração no startup dá privilégios de alteração de esquema à API |
| Backup SQL fora do volume do banco antes de migração | Permite restaurar o estado anterior em caso de incompatibilidade | Trocar somente a imagem exige compatibilidade do banco |
| Imagem de aplicação resolvida por digest e banco fixado por versão | Permite reproduzir a instalação e atualizar o banco em ciclo próprio | Tags flutuantes podem mudar entre instalações |

[Docker e loopback](https://docs.docker.com/engine/network/port-publishing/) · [SQL Server em contêiner](https://learn.microsoft.com/sql/linux/quickstart-install-connect-docker) · [Migrações EF Core](https://learn.microsoft.com/ef/core/managing-schemas/migrations/applying)

## Inicialização e certificados

Usar um health check SQL com consulta autenticada. No primeiro início, o Compose aguarda `service_healthy` para o SQL e `service_completed_successfully` para o migrador. Na atualização, recriar a execução do migrador com a imagem da nova versão, após parar a API e concluir o backup aplicável.

No Windows, gerar certificado com SAN localhost pelo PowerShell, confiar apenas no certificado público no perfil do titular e montar o PFX de HTTPS como somente leitura. O certificado usado para proteger as chaves de autenticação tem ciclo de vida próprio e deve permanecer disponível durante a restauração.

[Ordem de inicialização no Compose](https://docs.docker.com/compose/how-tos/startup-order/) · [HTTPS no ASP.NET Core com Compose](https://learn.microsoft.com/en-us/aspnet/core/security/docker-compose-https?view=aspnetcore-10.0)
