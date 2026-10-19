# Marco 30 — prévia e clique no recipiente

Branch `codex/container-route-preview`, base `09305bb`.

No modo Caminhar, apontar para um recipiente mostra a aproximação e os passos,
ou informa que não há caminho. Clicar seleciona o recipiente e solicita esse
mesmo trajeto. Ao alcance, orienta a consulta com J; não recolhe automaticamente.
Prévia, clique e Aproximar-se da janela J usam a mesma escolha de hex alcançável.

T enquadra o percurso incluindo o recipiente, como já fazia com portas. O botão
de enquadrar conserva o alvo ao passar pelo painel. Perto do recipiente não
mostra percurso fictício; consulta modal e modo Inspecionar limpam a prévia.

O pote 330 e um cenário 342 compartilham o hex 19868. O volume provisório do
cenário encobria o recipiente menor. A resolução de interação prioriza o
recipiente visível sobre cenário/item no mesmo hex do primeiro volume atingido.
Usa uma única consulta física por prévia/clique, com desempate por offset fonte.
Mantém os corpos e registros originais. Paredes, criaturas e portas continuam
resolvidas pelo primeiro volume; o ajuste não procura alvos em hexes atrás dele.

Validação: compilação sem avisos/erros; integração sem janela e gráfica isolada
com saída 0, `CONTAINER_ROUTE_OK`. Picking real do pote sobreposto, bloqueio com
primeira porta fechada, caminho aberto com prévia/clique idênticos, enquadramento
e restauração via painel, chegada sem coleta, consulta modal e limpeza. Portas,
itens, perícias e reconstrução dos bytes do MAP preservados. Regressões de
portas, piso, saídas, coleta, bolsa, devolução e checkpoint também passaram.

Captura `.local/temple-container-route.container-route.png`, logs
`.local/container-route-build.log`, `.local/container-route-preview.log`, stdout,
stderr e JSON da execução isolada. Resolução 1440 × 900; nenhuma janela no desktop
ativo. Os 134 grupos sintéticos do domínio/checkpoint seguem aprovados.

Próximo: solicitar aproximação ao recipiente de origem a partir da bolsa, quando
ele estiver na área atual. GPT-6.1 Sol, esforço Alto (High).
