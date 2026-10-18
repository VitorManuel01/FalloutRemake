# Décimo sétimo marco — inventário original no inspetor

Branch `codex/temple-inventory-inspector`, base `92a86eb`.

Selecione um recipiente, criatura ou objeto com conteúdo e use **I / Inventário**.
A janela mostra a árvore original, quantidades por recipiente, nomes e descrições,
PID, FID, ID, chave fonte e campos brutos de cada item. Conteúdo vazio também é
exibido. **I**, Fechar, Esc ou o X encerram a janela. A consulta suspende o avanço
da caminhada e os controles da câmera; o percurso continua disponível ao fechar.

É inspeção dos registros do MAP: sem distância de interação, transferência,
equipamento, capacidade ou inventário jogável do personagem. Quantidades aninhadas
não são multiplicadas. IDs repetidos continuam separados por chave/offset fonte.
Itens do inventário não são inseridos como objetos na cena. Sem alterar fontes,
sessão de portas, checkpoint ou XP.

`InventoryView` constrói linhas com pai, profundidade, quantidade e objeto original,
com limite de 1000 linhas e aviso explícito de truncamento. Profundidade, quantidade
e chaves são verificadas; a consulta não regrava os arrays fonte. A janela usa
subjanela embutida, duas colunas com larguras mínimas e área de detalhes rolável.

Validação: **109 grupos sintéticos**, três novos para árvore, quantidades,
IDs repetidos, limites e origem intacta. Compilação sem avisos/erros; Godot
gráfico e sem janela com `INVENTORY_INSPECTOR_OK`, saída 0, log gráfico de erros
vazio. Caso real com conteúdo e caso vazio; nomes/PID/chave, tecla/botão, entrada
da subjanela, fechamento, movimento suspenso sem cancelar rota, ator e contagem
de objetos intactos. Fontes reconstruídas byte a byte. Tamanho da janela e espaço
dos painéis conferidos automaticamente. Captura 1440×900
`.local/temple-inventory.inventory.png` revisada; logs `.local/inventory-*.log`.

Próximo: grade de navegação opcional para inspecionar o piso enquanto caminha.
Modelo: GPT-6.1 Sol, esforço Médio (Medium).
