# 001 · Tarefas

## Ordem de execução

Os itens seguem a ordem das dependências. As referências a US1 e US2 apontam para as histórias da especificação. As referências finais identificam os critérios de aceitação.

- [ ] T001 Fixar versões compatíveis e criar a solução, o frontend e os projetos de testes. [FND-01–04]
- [ ] T002 Configurar SQL Server, EF Core, migração de identidade e provisionamento local. Depende de T001. [FND-01]
- [ ] T003 [US1] Implementar login, logout, sessão, proteção CSRF e autorização por titular. Depende de T002. [FND-01]
- [ ] T004 [US2] Implementar modelo, migração e endpoints de cadastro, alteração, arquivamento e reativação. Depende de T003. [FND-02–04]
- [ ] T005 [US2] Criar telas de login e contas, incluindo estados de erro e sessão expirada. Depende de T003 e T004. [FND-01–04]
- [ ] T006 Verificar o fluxo com SQL Server e documentar execução e provisionamento local. Depende de T005. [FND-01–04]
