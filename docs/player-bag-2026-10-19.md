# Marco 28 — bolsa do personagem

Branch `codex/player-bag`, base `b3d7127`.

**B / Bolsa**, no cabeçalho, mostra os itens recolhidos na campanha, com nome,
quantidade, descrição e área de origem. Funciona sem selecionar um objeto.
Pilhas de fontes diferentes permanecem separadas, mesmo se tiverem o mesmo PID;
a chave original, ID e PID podem ser conferidos no tooltip do item.

O texto é resolvido pelo protótipo da área de origem, inclusive quando o
personagem está no exterior e a fonte é o templo. Os itens reaparecem depois de
carregar o checkpoint. A bolsa vazia orienta a coleta com J. O contador usa
inteiro de 64 bits para somar quantidades sem truncamento.

A consulta suspende caminhada e câmera, sem cancelar o percurso, e fecha com B
ou Fechar. Troca/reconstrução da área também a encerra. Não permite usar/equipar
itens, não transfere para outro proprietário e não altera os registros originais.

Validação: compilação sem avisos/erros; Godot sem janela e gráfico isolado com
saída 0 e `PLAYER_BAG_OK`. Tecla e botão, bolsa vazia, item real ID 106/PID 273,
quantidade e nome original, origem do templo após carregar checkpoint no
exterior, limites da janela e do cabeçalho, pausa modal/câmera e fonte intacta.
Os 130 grupos sintéticos do domínio/checkpoint continuam aprovados.

Captura `.local/temple-bag.bag.png` revisada em 1440 × 900. Logs
`.local/bag-build.log`, `.local/bag-preview.log`, `.local/temple-bag.stdout.log`,
`.stderr.log` e `.local/bag-isolated.json`. Apenas desktop isolado para imagens,
sem janela no desktop ativo.

Próximo: devolução ao recipiente de origem, com as mesmas regras de proximidade
e conservação. Modelo recomendado: GPT-6.1 Sol, esforço Alto (High).
