# Contrato operacional proposto

Executar os comandos PowerShell no diretório da instalação, com a mesma identidade Compose e configuração local.

| Comando | Entradas | Resultado |
| --- | --- | --- |
| `./Install.ps1` | Configuração local e referências de imagem | Valida os pré-requisitos, obtém imagens, espera o SQL, aplica migrações e disponibiliza a aplicação |
| `./Update.ps1` | Configuração existente e referência da nova versão | Obtém imagens, para a aplicação, gera backup antes de mudança de esquema, aplica migração e inicia a versão nova |
| `./Stop.ps1` | Configuração existente | Para serviços e conserva os volumes |
| `./Restore.ps1 -BackupPath <arquivo>` | Backup e referência compatível da versão anterior | Restaura com a API parada. A retomada depende de verificar banco e versão |

Os scripts previstos ficam em `scripts/container/` e integram o pacote de instalação. A configuração fornece imagem, credenciais de banco por função, caminhos de certificados e diretório de backup. Usar código de saída diferente de zero e mensagem em português na falha, sem expor segredos. Interromper a atualização antes de iniciar a nova API se houver falha de backup ou migração.

O endereço para navegador é `https://localhost:7043`. Os contratos existentes da API permanecem aplicáveis.
