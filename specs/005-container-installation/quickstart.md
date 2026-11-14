# Validação da instalação Docker

Executar após implementar os comandos do [contrato](contracts/installation.md). Usar Windows x64, Docker Desktop com WSL 2 em modo Linux e Docker Engine 28 ou superior, certificado confiável para localhost, configuração local e dados sintéticos. Usar um projeto Compose exclusivo para o piloto e duas versões de teste, com migração na segunda.

```powershell
docker info --format '{{.OSType}}/{{.Architecture}}'
docker version --format '{{.Server.Version}}'
docker compose version
./Install.ps1
docker compose ps
```

No navegador, abrir `https://localhost:7043`, criar conta, entrar e cadastrar um registro sintético. Validar o certificado e tentar acesso a partir de outro computador pela LAN. Confirmar acesso local e recusa externa. Conferir que a porta SQL não foi publicada.

| Caso | Ação | Resultado esperado |
| --- | --- | --- |
| Instalação limpa (SC-001, SC-004) | Executar o fluxo acima com volume SQL vazio e sem a imagem inicial no cache local, baixando-a do registro de teste | Banco e identidades provisionados, migrações aplicadas, HTTPS disponível e cadastro concluído. Reexecutar no mesmo volume e confirmar os dados preservados. Confirmar que a credencial da API opera e que o SQL rejeita uma alteração de esquema feita por ela |
| Recriação (SC-002) | Executar `./Stop.ps1`, remover contêineres do piloto com `docker compose down` sem remover volumes e executar `./Install.ps1` | Conta e registro continuam acessíveis |
| Atualização (SC-002) | Selecionar a segunda imagem por digest, baixá-la do registro de teste e executar `./Update.ps1` | Backup produzido, migração aplicada e registro preservado |
| Falha (FR-004 a FR-006) | Em instalação isolada, testar certificado inválido, SQL indisponível, falha de migração e Engine abaixo de 28. No caso do Engine incompatível, confirmar a mensagem específica em português e a interrupção antes de iniciar os serviços | Mensagem acionável, saída diferente de zero e nova versão indisponível |
| Recuperação (SC-003) | Em instalação isolada, selecionar versão compatível e executar `./Restore.ps1 -BackupPath <arquivo>`, depois iniciar a versão recuperada | Consulta retorna o registro salvo antes da atualização |

Executar os checks de build e testes de backend e Angular da CI. Antes do piloto, registrar as versões e os digests das duas imagens da aplicação e a referência fixada do SQL Server. Durante o piloto, registrar o resultado por caso e a evidência da restauração. Ao terminar, executar `./Stop.ps1` e confirmar que `docker compose ps --status running` não lista serviços do piloto.

## Imagens do piloto inicial

- Aplicação: `ghcr.io/carlosmancini/financemanager-pilot@sha256:d7e444b5f26c26289fd00f65b5b0aba1a0ece44a1e5608a41c0e9d1ffd96609d`.
- SQL Server: `mcr.microsoft.com/mssql/server@sha256:05644dad1266ca3d445d1e8f6d2ebb9a6a03df9a39376e0f7b48917356dca9d9`.
