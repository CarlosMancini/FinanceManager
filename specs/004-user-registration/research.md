# Pesquisa e decisões · 004

Pesquisa em 2026-11-12, baseada na spec esclarecida, código existente e documentação oficial. Não restam decisões técnicas bloqueantes.

## Stack e integração existente

- **Decision**: manter SDK 10.0.403, Identity/EF Core 10.0.14, xUnit 4.0.3 e Angular 22.2.5/TypeScript 6.0.3, já fixados no repositório. Sem novos pacotes.
- **Rationale**: `FinanceManagerDbContext` já reúne Identity, sessões e finanças; `OwnerAccessService` e adaptadores fornecem pontos de extensão.
- **Alternatives considered**: outro provedor, JWT e atualização da stack descartados por ausência de necessidade na spec.

## Validação e identidade

- **Decision**: regras puras de perfil/senha no domínio e adaptadores `IUserValidator<Owner>`/`IPasswordValidator<Owner>` reutilizando-as. Contar senha por `EnumerateRunes` em C# e iteração de pontos de código em TypeScript. Maiúsculas/minúsculas usam categorias Unicode; dígito obrigatório é ASCII; símbolo exclui letras, dígitos Unicode e espaços Unicode. Não aparar senha, nem aplicar a política nova à comprovação de senha legada.
- **Rationale**: validadores padrão não expressam sozinhos comprimento máximo e pontos de código da FR-007; e-mail não deve receber restrições adicionais à FR-005.
- **Alternatives considered**: regex somente no formulário ou política padrão Identity descartadas por divergência entre camadas.
- **Fonte**: [configuração Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-10.0). A adequação exata às regras da spec é decisão do projeto.
- **Decision**: preservar `Owner.Id`; novo cadastro usa `UserName` técnico gerado do ID. Legado mantém nome interno, aceito somente na prova enquanto incompleto. Login concluído busca exclusivamente e-mail com trim e caixa invariável, sem remover acentos, pontos ou sufixos.
- **Rationale**: e-mail/senha são credenciais; vínculos financeiros usam a identidade estável.
- **Alternatives considered**: recriar titular ou aceitar nome como alias permanente descartados por FR-009/016.

## Singleton e atomicidade

- **Decision**: slot 1 obrigatório, check igual a 1 e índice único. Transação explícita usa o mesmo DbContext do store Identity para perfil/hash/auditoria. Consulta prévia orienta UX; SQL decide concorrência. Após violação conhecida do índice, rollback antes de reler o estado em contexto limpo.
- **Rationale**: índice atual é único mas nullable; `AnyAsync` sozinho não protege dois registros simultâneos.
- **Alternatives considered**: trava em memória e criação parcial descartadas por corridas e rollback incompleto.
- **Fonte**: [transações EF Core](https://learn.microsoft.com/en-us/ef/core/saving/transactions). Se houver retry automático futuro, envolver a transação inteira, nunca gravações parciais.

## Autorização de transição

- **Decision**: token aleatório criptográfico de 32 bytes, cookie `__Host-FinanceManager-Registration` Secure/HttpOnly/SameSite=Strict/Path=/, sem Domain. Somente SHA-256 do token no SQL. Sem token em JSON/URL/localStorage e sem principal financeiro. Capturar `VerifiedAtUtc` via TimeProvider quando a verificação das credenciais antigas termina com sucesso, antes de aguardar bloqueio; `ExpiresAtUtc = VerifiedAtUtc + dez minutos`, sem renovação. Espera por bloqueio, persistência e envio não reiniciam o prazo; se ele terminar antes da emissão, exigir nova comprovação sem entregar cookie utilizável.
- **Rationale**: autoriza somente a conclusão do cadastro e pode ser invalidado imediatamente no servidor.
- **Alternatives considered**: sessão financeira provisória e token autocontido sem revogação SQL descartados por FR-015–017.
- **Decision**: emissão e conclusão usam bloqueio de atualização na linha do titular (`UPDLOCK, HOLDLOCK`, somente na infraestrutura) em transação curta. Após prova da senha, emissão revalida estado incompleto e versão da credencial sob o bloqueio. Conclusão toma o mesmo bloqueio, relê autorização, verifica prazo no ponto da mutação, altera perfil/hash/conclusão, revoga todas as sessões/autorizações e audita antes do commit. Preservar verificação de `ConcurrencyStamp`; conflito faz rollback, sem retry sobre cadastro concluído.
- **Rationale**: duas conclusões não sobrescrevem o vencedor; prova iniciada antes de concluir não emite autorização utilizável depois. Cálculo do hash pode preceder seção crítica, mas estado, versão e tempo são revalidados nela.
- **Fonte**: [concorrência EF Core](https://learn.microsoft.com/en-us/ef/core/saving/concurrency). A coordenação explícita por linha é decisão do projeto a validar no SQL real.

## Sessões, CSRF e limitação

- **Decision**: revogação explícita de todas as linhas `OwnerSessions` na transição. `OwnerTicketStore.RetrieveAsync` e admissão rejeitam titular incompleto; emissão de sessão exige cadastro concluído. Preservar 30 minutos de inatividade.
- **Rationale**: tickets atuais são reconstruídos do SQL sem verificar security stamp; alterar somente esse stamp não revoga os cookies existentes.
- **Alternatives considered**: confiar no Angular ou na expiração natural dos cookies descartado por permitir chamadas diretas.
- **Decision**: compartilhar política `login` de dez tentativas/minuto por IP entre login e prova; manter bloqueio Identity de cinco falhas/cinco minutos. Todas as mutações continuam sob filtro CSRF, inclusive `AllowAnonymous`. Renovar token CSRF após mudança de identidade e obter token anônimo antes do próximo login.
- **Rationale**: o filtro atual já protege mutações anônimas; não criar bypass no cadastro. Adaptar interceptor Angular para incluir obrigações e distinguir autorização expirada de sessão financeira expirada.

## Migração, provisionamento e limites

- **Decision**: migração aditiva deixa legado incompleto, preservando ID/hash/FKs. Se houver mais de um titular, abortar sem remover/reagrupar dados. Retirar criação via `provision-owner` de produção, orientando cadastro web; fixtures legadas preparam migração anterior.
- **Rationale**: não inventar perfil/e-mail nem permitir caminho que dispense cadastro obrigatório.
- **Alternatives considered**: recriar banco, atribuir e-mail fictício ou continuar provisionando incompletos em produção descartados por FR-008/016.
- **Decision**: não inventar limites de tamanho para nome/apelido/e-mail. Usar `nvarchar(max)` nesses campos e sobrepor o mapeamento limitado de Email/NormalizedEmail do Identity, removendo explicitamente o índice padrão `EmailIndex` sobre NormalizedEmail na migração. Comparar normalização com collation binária; sem índice textual ilimitado: singleton já impede um e-mail identificar dois titulares. Telefone guarda 10–15 dígitos e `+` inicial quando informado.
- **Rationale**: preservar critérios da spec e compatibilidade SQL sem índice/hash adicional para um único titular. Limites HTTP de infraestrutura permanecem distintos da validação de campo.
- **Alternatives considered**: impor limite silencioso de 256 caracteres e criar outra identidade descartados.
- **Decision**: dados não secretos permanecem somente em memória no formulário; expiração pede nova prova sem apagá-los. Senhas descartadas ao sair/sucesso, sem transporte ao login. Auditoria mínima, sem PII/segredos.
- **Rationale**: FR-014/017 e privacidade; nenhuma persistência de formulário no navegador.
