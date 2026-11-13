# Evidências de validação · 004

## Fase 1 · T001 — baseline validado

- Checklist de requisitos: 16/16 itens marcados.
- SDK .NET 10.0.403 e dotnet-ef 10.0.14 correspondem aos manifestos. `dotnet tool restore` e `dotnet restore .\FinanceManager.slnx --locked-mode` passaram com acesso à configuração NuGet do usuário.
- Node global 24.13.0 diverge do 24.22.2 fixado. O Node 24.22.2 foi instalado em `.local-tools/node/`, diretório ignorado pelo Git, sem alterar manifestos ou lockfiles.
- A restauração Angular foi concluída pelo `npm-cli.js` executado diretamente com o Node 24.22.2: `npm ci --no-audit --no-fund` instalou 280 pacotes. `npm ls --depth=0` passou e confirmou as versões diretas do manifesto. Tentativas anteriores interrompidas foram substituídas por essa restauração concluída.
- `.gitignore` existente cobre os artefatos da stack; nenhum hook de extensão configurado foi encontrado.

T001 concluída.

## Fase 1 · T002 — parcialmente validada

- Criadas fixtures para banco vazio e para titular legado com hash, sessão, conta, movimentação, obrigação e baixa sintéticos na migração 003.
- `dotnet build` do projeto de integração passou sem avisos ou erros. Dois testes das fixtures passaram em bancos LocalDB isolados (2/2), incluindo verificação de senha sintética e vínculos financeiros.
- Após a geração da migração 004, foi adicionada a fixture de titular com cadastro concluído. A fixture legada insere a identidade usando as colunas da migração 003 antes de aplicar a migração 004, sem depender do modelo EF atualizado.
- Os três testes dos cenários vazio, concluído e legado passaram no LocalDB. T002 foi marcada como concluída.

## Fase 2 · validação

- T003–T014 concluídas. As regras de perfil, senha e autorização incluem limites, caracteres Unicode, expiração absoluta, revogação e virada do dia em São Paulo. A suíte unitária completa passou: 82 testes.
- A migração foi validada em LocalDB para banco vazio e atualização do esquema 003. Testes verificaram preservação de identidade e vínculos financeiros, rejeição de múltiplos titulares legados, slot único, perfil obrigatório, prazo e hash da autorização, auditoria e remoção do índice antigo de e-mail antes da alteração da coluna. `dotnet ef migrations has-pending-model-changes` confirmou que o snapshot corresponde ao modelo.
- Validadores Identity foram testados com e-mail longo, senha Unicode e senha legada; o contrato HTTP de validação foi verificado em quatro testes, inclusive CSRF em mutação anônima. Sessões incompletas foram testadas com cookie legado real nas três rotas financeiras, com controle de sessão válida e expiração aos 30 minutos.
- A suíte completa de integração passou em SQL Server LocalDB: 40 testes. A suíte Angular existente passou: 25 testes em quatro arquivos. A suíte de integração de contas foi adaptada para verificar leitura e escrita com identificador de titular diferente sem inserir um segundo titular, proibido pela nova restrição.
- A fase 2 prepara a fundação. Os fluxos de cadastro, login definitivo e transição pertencem às fases seguintes e não foram implementados nesta fase. A atualização não deve ser disponibilizada a instalações legadas antes da conclusão da transição.

## Fase 3 · US1 — cadastro inicial

- T015–T025 concluídas. O cadastro inicial valida os dados no serviço, persiste titular, hash e auditoria na mesma transação e não cria sessão. O endpoint orienta login quando já existe cadastro e transição quando há titular legado.
- Testes no SQL Server LocalDB cobriram campos obrigatórios e entrada inválida, CSRF, dois cadastros concorrentes com e-mails iguais e diferentes, rollback de falha na auditoria, repetição após resposta perdida e preservação após reiniciar a aplicação. O titular legado e sua baixa permaneceram intactos.
- A interface foi validada com dados sintéticos em base local isolada: campos vazios exibiram mensagens, critérios de senha foram apresentados e o cadastro válido retornou ao login com confirmação e senha vazia. O contrato HTTP também verificou ausência de sessão após cadastro.
- Suítes finais: 94 testes unitários, 54 de integração e 32 testes Angular passaram. O build Angular passou. O comando `provision-owner` retorna orientação para criar a conta pela interface, sem provisionar titular.
- Esta validação cobre somente US1. Login definitivo por e-mail pertence à US2, e a transição de instalações legadas pertence à US3; não disponibilizar a atualização a instalações legadas antes de concluir essas histórias.
