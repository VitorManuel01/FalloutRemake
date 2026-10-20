# Recuperação manual do baú: caminho nativo conferido

Continuação do levantamento estático, na branch
`codex/temple-chest-recovery-profile`. Não executa scripts nem habilita o desafio.

O MAP Epic mantém o baú do offset 465604 vazio, com flags de recipiente zero
(destrancado), frame zero e PID 245. O PRO de 65 bytes tem hash
`AF59B61FF1E07313112E5160F6DA59E54F4DF3D82E0952409F01CE3A0FB32CA6`.
O FID 131 resolve `art/items/chest1.frm`: cabeçalho v4 com quatro frames,
6.429 bytes, hash `4F509889BEB9D1E828DE6BFEB63B3257ED026F3D85939A6307A4828B5DE99803`.
Somente metadados do cabeçalho foram lidos; nenhuma arte foi extraída ou gerada.

No Community Engine local (`e97087b9582f37075db347a89898887320753f8b`):

1. `actionPickUp` (`src/actions.cc`) aproxima o personagem e, para recipiente
   fixo, solicita abertura e coleta.
2. `_obj_use_container` (`src/proto_instance.cc`) recusa tranca, chama o
   procedimento de uso e, sem override, solicita a animação de abrir/fechar.
3. `scriptsRequestLooting` e o processamento da solicitação (`src/scripts.cc`)
   encaminham à janela de inventário.
4. `inventoryOpenLooting` (`src/inventory.cc`) recusa recipiente ainda fechado
   com mais de um frame, chama pickup e respeita override. A retirada individual
   chama `itemMove(targetObj, player, quantidade)`; retirar tudo verifica carga
   antes de `itemMoveAll(target, player)`.

Ausência de `use_p_proc`/`pickup_p_proc` não significa simplesmente pular código:
`scriptLocateProcs` grava zero para procedimento ausente; `scriptExecProc`
reinicia `scriptOverrides` e usa índice 1 como fallback. No programa conhecido,
índice 1 é `checkPartyMembersNearDoor`, corpo físico 754, compartilhado com
o registro auxiliar. O programa inteiro não contém `script_overrides` (0x80B9).
As verificações do relatório incluem esses fatos, o PRO, flags e cabeçalho FRM.

Conclusão limitada: existe um caminho nativo de **recuperação manual**, sem
necessidade de uma transferência inversa em `actemvil.int`, quando o recipiente
está aberto, ao alcance e sem override. Ainda não foi demonstrada a sequência
completa de estados do desafio, nem a disponibilidade de acesso após o duelo.
O relatório diferencia explicitamente essas duas afirmações.

Reprodução: `--temple-chest-report` do TempleInspector, usando os dados Epic
locais já configurados. O comando valida os hashes e a reconstrução do MAP.
Evidência local: `.local/chest-native-recovery.json`.
Validação deste incremento: relatório Epic com reconstrução verificada e
227 grupos sintéticos aprovados; nenhuma rotina de jogo foi executada pelo relatório.

Próximo incremento: explicar a função deste baú na interface, mantendo as ações
bloqueadas até existir suporte à custódia dos itens e aos estados do desafio.
Modelo recomendado: GPT-6.1 Sol, esforço Alto.
