# 002 · Tarefas

## Ordem de execução

Os itens seguem a ordem das dependências. As referências a US1 e US2 apontam para as histórias da especificação. As referências finais identificam os critérios de aceitação.

- [ ] T001 Modelar movimentação, regras monetárias e temporais e migração. Depende de 001/T006. [MOV-01, MOV-03]
- [ ] T002 [US1] Implementar criação, correção e cancelamento com controle de concorrência. Depende de T001. [MOV-01, MOV-03]
- [ ] T003 [US2] Implementar consulta paginada e totais com os mesmos filtros. Depende de T001. [MOV-02, MOV-04]
- [ ] T004 [US2] Criar formulário, histórico e filtros no Angular. Depende de T002 e T003. [MOV-01–04]
- [ ] T005 Verificar API, banco e fluxo completo de lançamento e correção. Depende de T004. [MOV-01–04]

A proteção do vínculo de baixa em MOV-03 é entregue e verificada na feature 003.
