# Marco 41 — ícones na coleta e bolsa

As janelas J/B apresentam o ícone original ao lado do nome e uma imagem maior
junto da descrição. Preservam a proporção e transparência, usando filtro nearest.
O FID de inventário vem do protótipo da área de origem, inclusive após viajar
e carregar um checkpoint no exterior.

Texturas são criadas em memória; o cache usa FID, caminho, hash FRM e hash da
paleta, limitado a 512 imagens. Falha de leitura/decodificação deixa o item
com texto e ações usuais. Uma falha por FID é registrada uma vez por abertura
dos dados. Quantidades e checkpoint não recebem arte ou novos campos.

Validação: build sem avisos/erros, 156 grupos sintéticos; suíte Godot completa
sem janela e em desktop isolado, ambas com saída 0. `INVENTORY_ART_UI_OK`
confere dimensões 47×63 e FID do pó, reutilização da textura, falta simulada do
recurso sem bloquear a ação e limpeza do ícone ao esvaziar a janela. Coleta e
bolsa conferem o ícone real, incluindo bolsa com origem em área inativa.

Capturas de J/B revisadas em 1440×900. Runner isolado: 19,9 s, stderr vazio,
desktop de entrada Default antes/depois. Assets originais e capturas fora do Git.

Próximo: conferir a coleta e os limites de acesso nas três elevações do templo,
antes de ampliar os adaptadores. GPT-6.1 Sol, esforço Alto.
