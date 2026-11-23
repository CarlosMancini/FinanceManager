# Contrato de interface — 006 · Atualizar Cadastro

**Revisão**: 2026-11-22, esclarecimento B de conflito por campo editado.

## Entrada e navegação

Na navegação autenticada de `src/frontend/src/main.ts`, adicionar link “Atualizar Cadastro” junto a Movimentações, Contas a pagar e Contas. Destino `/profile`, título “Atualizar Cadastro”. Guard somente requireSession; nenhum guard de contas financeiras. Acesso direto exige sessão válida; usuário sem conta financeira também consegue usar a tela. Preservar navegação e regra de destino após login existentes.

## Estados

| Estado | Comportamento |
|---|---|
| Carregando | Indicar carregamento; não liberar envio nem apresentar campos vazios como dados existentes |
| Falha de consulta | Mensagem em português e opção “Tentar novamente”; sem formulário editável até consulta válida |
| Pronto | Preencher cinco campos pessoais com GET; criar snapshot em memória; seção de senha recolhida |
| Alterar senha acionado | Exibir exatamente três campos secretos vazios; nenhuma chamada HTTP, validação ou gravação |
| Conflito de edição | Informar campos conflitantes; manter snapshot inicial e rascunho pessoal; oferecer consulta para revisão, sem rebase/reenvio automático |
| Enviando | Congelar envio e impedir duplicação; sem abandonar dados preenchidos |
| Erro corrigível | Exibir mensagens por campo e manter dados pessoais preenchidos; permitir corrigir e reenviar |
| Atualizado | Mostrar confirmação; substituir formulário/snapshot pelo profile retornado; limpar senhas e recolher seção |
| Sem mudanças | Exibir informação de ausência de alterações; atualizar snapshot com profile vigente sem alegar gravação |
| Sessão inválida | Encaminhar ao login pelo mecanismo existente; descartar segredos |

## Campos e envio

Mesma ordem e labels de register para Nome completo, Apelido (opcional), Data de nascimento, E-mail e Telefone. Telefone com placeholder `(DDD) + Telefone`, somente números na entrada, máscara `(DD) XXXXX-XXXX` e primeiros 11 dígitos em digitação/colagem; aceitar o preenchimento a partir dos dígitos retornados. Data civil sem conversão de fuso na exibição.

Depois dos dados pessoais, botão tipo button “Alterar senha”. Ao acionar, renderizar Senha atual, Nova senha e Confirmar nova senha, com type password, autocomplete current-password para a atual e new-password para os demais. Não recuperar credencial existente. Não é necessário controle adicional de desfazer/recolher antes de enviar nesta entrega; ao abandonar ou concluir, descartar todos os segredos.

Exibir instrução estática de 12 a 128 caracteres, maiúscula, minúscula, número e símbolo. Não mostrar checks de critério atendido/não atendido durante digitação. No final de todo o formulário, botão submit “Atualizar cadastro”; durante envio, “Atualizando…”. Formulário usa novalidate para evitar validação nativa antecipada ou mensagens fora do contrato.

Input/blur e abertura da seção não executam validação de preenchimento nem consultam e-mail/senha. Máscara/limite são tratamento de entrada, sem mensagem de validade. Erros de tentativa anterior podem ser limpos durante edição, mas novos resultados só aparecem no próximo submit. Submit por teclado segue o mesmo caminho do botão.

Ao enviar:

1. Validar os dados pessoais completos e, se seção acionada, os três campos secretos. As funções puras reutilizadas de register não podem impor senha quando seção não acionada.
2. Em erro local, mostrar orientação por campo e focar o primeiro erro; não chamar PATCH. Senha atual só pode ser comprovada pelo servidor no PATCH, nunca em consulta separada.
3. Calcular changes contra o snapshot inicial após normalização e originalValues contendo os valores carregados somente das mesmas chaves editadas. Ambos têm exatamente as mesmas chaves e são enviados mesmo vazios. Campo revertido é omitido dos dois; apelido apagado gera changes.nickname null e originalValues.nickname com o valor inicial (ou null se ausente). E-mail equivalente por identidade normalizada não compõe mudança; telefone é comparado em dígitos. Cobrir paridade das normalizações com o domínio, inclusive Unicode, sem reduzir formatos aceitos pelo cadastro.
4. Enviar sempre o contrato de escrita com changes e originalValues, inclusive ambos vazios, para receber updated/unchanged ou conflito. O servidor compara originais antes de considerar no-op, mesmo quando o valor pretendido já foi gravado por outra aba. Incluir passwordChange somente se usuário acionou a opção. Não inferir senha inalterada antes da comprovação no servidor.
5. Sucesso atualiza snapshot/formulário e e-mail corrente de AuthService usando a resposta confirmada; não disparar login novo ou logout. Não confundir falha posterior de atualização visual com falha de commit já confirmado.

## Tratamento dos retornos

400/409 por campo são apresentados junto ao campo correspondente. 409 PROFILE_UPDATE_CONFLICT rejeita todo o envio, inclusive senha, e orienta revisar os dados atuais. Conservar snapshot inicial e rascunho pessoal; não atualizar automaticamente os originais para que o próximo clique vença silenciosamente o conflito.

A ação explícita “Revisar dados atuais” executa GET e guarda o perfil atual separadamente do rascunho. Apresentar valores atuais e rascunho para os campos em conflito e permitir escolher os atuais ou reaplicar os do rascunho. Somente após essa decisão explícita, atualizar a referência para o perfil consultado e recomputar changes/originalValues. Para campos que o usuário não editou, adotar os atuais e não gerar delta. A revisão nunca salva por si só: exige novo clique em “Atualizar cadastro”; alterações posteriores à revisão continuam sujeitas à mesma comparação no servidor.

Descartar campos de senha ao iniciar a revisão de conflito e exigir preenchimento/prova da credencial vigente se o usuário mantiver a intenção de trocar senha. Não reaplicar segredos automaticamente. Se o GET de revisão falhar, conservar rascunho/referência anteriores, informar erro e não liberar envio com originais falsamente renovados.

403 orienta atualizar contexto CSRF e tentar explicitamente; 429 informa aguardar sem retry; 500/falha de rede não presume sucesso. Após resposta perdida, consultar/revisar explicitamente antes de repetir. Replay do PATCH antigo pode retornar conflito mesmo com os valores desejados já gravados; isso não comprova qual requisição os gravou. Não exibir “atualizado” sem confirmação nem renovar originais/repetir PATCH automaticamente.

401 do GET/PATCH deve integrar sessionExpiredInterceptor e encaminhar ao login; senha atual incorreta usa 400 e permanece na tela. Senhas ficam somente em memória e podem ser mantidas durante correção na mesma tela; obrigatoriamente descartadas no sucesso, abandono, expiração ou destruição do componente. Nunca usar localStorage, sessionStorage, URL ou logs para o formulário.

## Acessibilidade e limites

Manter interface responsiva e estilo existente, labels associados, descrição dos critérios, aria-describedby para erros, role alert/status para resultados e foco previsível após submit. Testar teclado e viewport estreita. Nenhuma alteração de campos ou validações do cadastro inicial e conclusão legada, exceto extração mínima de funções puras com regressão.

## Aceitação

Validar FR-001–014 pela [matriz](../plan.md#matriz-de-verificação) e [quickstart](../quickstart.md), incluindo abrir com zero contas, comparação entre duas sessões: campos disjuntos preservados, mesmo campo rejeita tudo (inclusive desired==current e senha combinada), revisão/reaplicação explícita e falha de GET de revisão, botão no final, três senhas condicionais, ausência de validação antecipada, descarte de segredos e login com os valores novos.
