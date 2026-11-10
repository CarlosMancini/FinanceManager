# 003 · Tarefas

## Ordem de execução

Os itens seguem a ordem das dependências. As referências a US1 e US2 apontam para as histórias da especificação. As referências finais identificam os critérios de aceitação.

- [ ] T001 Modelar obrigação e baixa, criar migração com restrições e implementar cálculo de situação. Depende de 002/T023. [PAG-01, PAG-02, PAG-05]
- [ ] T002 [US1] Implementar cadastro, edição, cancelamento, filtros e totais em aberto. Depende de T001. [PAG-01, PAG-02]
- [ ] T003 [US2] Implementar baixa por vínculo ou criação, incluindo idempotência e controle de concorrência. Depende de T001 e T002. [PAG-03, PAG-05]
- [ ] T004 [US2] Implementar reversão e proteção de edição e cancelamento dos registros vinculados. Depende de T003. [PAG-04, PAG-05, MOV-03]
- [ ] T005 [US2] Criar telas de acompanhamento, baixa e reversão. Depende de T002–T004. [PAG-01–05]
- [ ] T006 Verificar fluxo de ponta a ponta, datas, isolamento, replay, concorrência e rollback com SQL Server. Depende de T005. [PAG-01–05]
