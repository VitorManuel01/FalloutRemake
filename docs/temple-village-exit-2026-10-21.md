# Porta original e caminhada até Arroyo

Depois de aplicar a conversa à partida e concluir a saída do NPC, o painel
oferece **Ir à porta de Arroyo**. O personagem percorre os hexes alcançáveis,
seleciona a porta e aguarda **U**. Depois, **Ir à saída de Arroyo** guia até
um marcador original; **Enter** confirma a viagem. Não há abertura ou viagem
automática. Ocupação, movimento e callbacks de uma sessão anterior são recusados.

A primeira tentativa com os dados reais revelou uma passagem ainda fechada:
porta **100**, offset **423748**, hex **13528**, elevação **2**. Ela usa
`aitemdor.int`, mas não é a porta 104 do desafio. O trecho **5912–6014** de
`map_update_p_proc` libera sua tranca quando **M20=2** e **G369=0**.
O GAM verificado identifica G369 como `GVAR_DUMAR_STATUS`, linha 378, valor
inicial zero. O domínio atual não oferece operação que altere essa variável.
Essa restrição é explícita; não se trata de uma VM geral ou suporte a saves
originais/mods.

O adaptador exige MAP, PRO, dono do script, SID, scripts.lst, GAM e INT originais,
além da sequência exata de instruções. A aceitação verificada da conversa é a
única autorização desta porta. U abre/fecha sem XP; arrombamento continua
indisponível. Porta, IDs, flags de origem e bytes proprietários não são alterados.

Checkpoint **v7 / neutral-v7** preserva a abertura da porta, inclusive com o
personagem sobre seu hex. Arquivos v1–v6 continuam migrando; v6 com conversa
aceita e porta fechada permanece fechado. Porta aberta em versão antiga,
conversa ausente/inconsistente ou outra identidade é recusada antes da troca
da sessão. O formato separado dos ensaios Speech/NPC mantém a versão existente.

Os três marcadores originais em **13326/13327/13328** apontam para
**mapa 4, elevação 0, hex 20517, rotação 0**. A consulta não carrega Arroyo nem
muda checkpoint, itens, posição ou XP. O destino só é carregado pela interação
de travessia existente. O checkpoint em Arroyo também conserva o templo inativo.

Validações desta etapa: **309 grupos sintéticos**, builds sem avisos/erros,
integração real `TEMPLE_VILLAGE_ROUTE_OK` e captura isolada 1280×800 aprovada
em `.local/village-v7-isolated.json`, entrada `Default` preservada. A integração
conserva os dois itens e 25 XP do cenário de teste e reconstrói os MAPs originais.
Evidência da condição: `.local/temple-exit-door-proof.json` (apenas local).
Bateria completa **18/18** aprovada:
`.local/temple-validation-20261021-041247-b8f5fee8/manifest.json`.

Limitação observada: `arvillag.int` possui recompensas na primeira entrada;
elas ainda não são executadas. A ausência de XP adicional nesta prévia não
pretende reproduzir a recompensa completa do jogo original. Antes de implementá-la,
auditar a guarda de primeira entrada, os ramos de G10 e os demais efeitos.
Modelo recomendado para esse próximo passo: **GPT-6.1 Sol, esforço Alto**.
