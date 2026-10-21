# Projeção neutra da primeira reação

O relatório da anciã agora confere o preâmbulo original para um perfil de
auditoria explícito: INT/CAR 5, nenhum Presence/Sex Appeal, dia 0, sem drogas
ou equipamento que altere atributos, primeira conversa após a entrada neutra
verificada. É uma projeção separada; esse personagem completo ainda não existe
no runtime e nenhum efeito é aplicado à partida.

O resultado inicial é **32**: `(CAR 5 − CAR da anciã 5) × 5 = 0`, mais
`reputação 65 / 2 = 32`, com divisão inteira. Wanderer, Slaver, Presence e
Childkiller contribuem zero nesse perfil. O temporário 24 do programa começa
em zero e permanece zero nessa passagem. Temporários do programa, locais do
script e globais do jogo são mantidos como categorias distintas.

A projeção da saudação tem locais 0–9:
`[32, 0, 1, 0, 1, 0, 0, 0, 0, 0]`. G531 passa a 1; G619, que já era 1 no
GAM original, continua 1. As guardas verificadas selecionam Node001.

Além dos hashes, o auditor confere a inicialização de 27 temporários,
políticas do preâmbulo, cache de carisma, sinal/divisão da reputação,
condições de trait/perk, seleção de karma, flags e armazenamento final da
reação. Não cobre conversas repetidas, todos os perfis de personagem ou a
concessão de itens de Node015.

Validação real: `ARROYO_ELDER_NEUTRAL_REACTION_AUDIT_OK` dentro de
`.local/arroyo-elder-neutral-reaction-audit.json`, build sem avisos/erros e
reconstrução original verificada. A última bateria completa anterior passou
**332 grupos / 20 etapas**, manifesto
`.local/temple-validation-20261021-162025-e091b67c/manifest.json`.

Semântica CE na revisão `e97087b9582f37075db347a89898887320753f8b`:
`interpreter_extra.cc/opHasTrait`, `perk_defs.h` (Presence=10),
`trait_defs.h` (Sex Appeal=13), `stat_defs.h` (Charisma=3) e `stat.cc`.
O código original local não é executado ou copiado para o Git.

Próximo marco: ensaio de estado separado para os caminhos textuais já
habilitados, verificando os decréscimos de reação, inclusive a ponte
Node006a→Node008, antes de qualquer commit ativo. **GPT-6.1 Sol, esforço Alto**.
