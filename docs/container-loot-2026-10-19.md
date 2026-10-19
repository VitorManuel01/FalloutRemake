# Marco 27 — recolher do recipiente

Branch `codex/container-loot-ui`, base `40fb216`.

Selecione um recipiente no modo Inspecionar e use **J / Recolher**, no painel de
navegação. A janela mostra os nomes originais e a quantidade restante na sessão.
**Retirar 1** ou **Retirar esta pilha** transfere para a bolsa somente ao alcance,
parado, na mesma área/elevação e dentro do perfil validado. Reabrir não duplica
itens. **I / Inventário** continua sendo a consulta aos registros originais.

Se estiver longe, **Aproximar-se** fecha a janela e inicia um percurso até o hex
alcançável mais próximo do recipiente, sem coletar automaticamente. Depois de
chegar, abra novamente com J. Não oferece aproximação quando não há caminho.
No pote do primeiro teste, é necessário abrir a primeira porta antes.

A janela suspende o avanço e a câmera, sem cancelar o caminho. Durante uma
caminhada, informa que é necessário fechar e aguardar; não permite transferir.
J ou Fechar encerram a consulta. Uma reconstrução da cena também a encerra,
evitando ações sobre referências antigas. K salva as retiradas após fechar.

Validação: compilação com zero avisos/erros. Integração sem janela e gráfica
isolada com saída 0, `CONTAINER_LOOT_OK`, cobrindo seleção/tecla/botão, ausência
de caminho, aproximação, pausa modal, Healing Powder ID 106, esgotamento,
repetição, checkpoint/reabertura e preservação de ator, portas e objetos raiz.
MAP reconstruído byte a byte. Os 130 grupos sintéticos anteriores continuam
aprovados; não houve alteração das regras do domínio neste marco.

Captura `.local/temple-loot.loot.png` e logs `.local/loot-build.log`,
`.local/loot-preview.log`, `.local/temple-loot.stdout.log`, `.stderr.log` e
`.local/loot-isolated.json`. Dados e capturas fora do Git. Validação em 1440 × 900,
sem abrir janela no desktop ativo.

Próximo: bolsa do personagem com nomes, quantidades e origem mesmo em outra área.
Modelo recomendado: GPT-6.1 Sol, esforço Alto (High).
