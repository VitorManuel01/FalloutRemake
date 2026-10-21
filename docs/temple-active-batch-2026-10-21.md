# Bateria: conversa aplicada à partida e chegada a Arroyo

Branch `codex/temple-active-speech`, baseada em `codex/temple-npc-departure`.

| Marco | Resultado |
| --- | --- |
| Estado ativo e checkpoint v6 | Conversa aceita, variáveis limitadas, pose/fase/evento do NPC e porta 104 passam a formar um estado validado conjuntamente. |
| Interface da conversa ativa | Ação explícita **Aplicar à partida**; a malha existente, seleção, aproximação e bloqueio acompanham o NPC sem mudar o MAP. |
| Auditoria de viagem | Escadas reais, elevação diferente, saída externa e retorno preservam evento e progresso; ocupação interrompe o trecho no último hex válido. |
| Avanço automático opcional | Desligado por padrão; pausa durante caminhada, janelas e inspeção de outra camada. Espera nativa de 20 ticks, limite de trabalho por frame e pausa ao salvar/carregar. |
| Visualização do percurso | Traçado lilás do trecho atual, primeira obstrução em vermelho, enquadramento seguro e filtros; consulta sem efeitos na partida. |
| Porta da vila e checkpoint v7 | Instância 100 validada pela condição original; aproximação real, U manual, persistência e migração restrita de versões anteriores. |
| Caminhada e viagem a Arroyo | Marcadores originais, Enter explícito, restauração em Arroyo e preservação do templo inativo, itens, XP e fontes. |
| Auditoria da recompensa de entrada | Guarda original de primeira entrada e três ramos de XP verificados; Speech corresponde a 600 XP base, ainda sem concessão na partida. |

O conjunto possui **309 grupos sintéticos**. As verificações gráficas foram
feitas somente no desktop isolado; o desktop de entrada permaneceu `Default`.
Capturas locais da última etapa: `.local/village-v7-1280.temple-village-door.png`
e `.local/village-v7-1280.arroyo-after-speech.png`.
Bateria completa **18/18**, builds sem avisos/erros:
`.local/temple-validation-20261021-041247-b8f5fee8/manifest.json`.

Detalhes e evidências por etapa:
[estado ativo](temple-world-v6-2026-10-21.md),
[interface](temple-active-speech-ui-2026-10-21.md),
[viagem](temple-active-travel-audit-2026-10-21.md),
[avanço automático](temple-npc-playback-2026-10-21.md),
[percurso](temple-npc-route-view-2026-10-21.md),
[porta e vila](temple-village-exit-2026-10-21.md),
[recompensa de entrada](arroyo-entry-reward-audit-2026-10-21.md).

Arte final, combate geral, desafio do baú e execução completa dos scripts da
vila continuam pendentes. A entrada na vila ainda não concede a recompensa
original. Próximo incremento recomendado: auditar sua condição de primeira
entrada e os ramos de recompensa antes de integrar XP. **GPT-6.1 Sol, esforço Alto**.

Arquivos proprietários permanecem fora do Git. Alterações anteriores de
formatação, `.uid` e `Assets/` são preservadas. Nenhum push foi solicitado.
