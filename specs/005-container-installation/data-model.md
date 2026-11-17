# Estado da instalação

| Recurso | Campos e vínculos | Ciclo de vida e validação |
| --- | --- | --- |
| Instalação local | Identidade estável do projeto Compose, referências às imagens, configuração externa | Reutilizar a identidade e os volumes ao atualizar. Validar configuração antes de alterar serviços |
| Banco | Volume SQL, banco FinanceManager e histórico de migrações EF | Nasce vazio. O histórico registra migrações aplicadas. A API só entra em serviço após migração bem-sucedida |
| Chaves de autenticação | Volume Data Protection e certificado de proteção persistido externamente | Reutilizar o conjunto ao recriar serviços e restringir acesso ao material privado |
| Backup | Arquivo SQL externo ao volume, versão anterior e data | Registrar origem e verificar restauração em banco isolado. Proteger o arquivo contra acesso indevido |

A feature usa o modelo de domínio e as migrações versionadas do FinanceManager.
