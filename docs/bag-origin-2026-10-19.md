# Marco 31 — voltar ao recipiente pela bolsa

Branch `codex/bag-source-approach`, base `a7b582c`.

Selecione uma pilha na bolsa B e use **Voltar ao recipiente**. Quando a origem
está na área/elevação atual do personagem e existe caminho, a janela fecha,
seleciona o recipiente e solicita a aproximação. A inspeção de outra camada
retorna à camada do personagem. O caminho usa a mesma regra da prévia, clique e
janela J: hex alcançável mais próximo, sem atravessar saídas automaticamente.

Ao chegar, B permite devolver explicitamente; não devolve durante o percurso.
O botão fica desativado ao alcance, em movimento, sem caminho ou com origem em
outra área/elevação. A consulta da bolsa não carrega mapas nem inicia viagens.

Validação: compilação sem avisos/erros. Integração sem janela e gráfica isolada
com saída 0, `BAG_ORIGIN_OK`: pote real/Healing Powder, bolsa durante inspeção
da elevação 1, retorno à elevação 0 sem teletransportar o personagem, mesma
sequência de passos, seleção, chegada liberando devolução, recusa de ação remota
no exterior sem carregar áreas, portas/itens/bytes originais preservados.
Regressões de todos os marcos anteriores também passaram. Sem novas regras de
inventário; os 134 grupos sintéticos existentes continuam aprovados.

Captura `.local/temple-bag-origin.bag-origin.png`, logs
`.local/bag-origin-build.log`, `.local/bag-origin-preview.log`, stdout/stderr e
`.local/bag-origin-isolated.json`. Imagens em 1440 × 900, somente no desktop
isolado, sem janela no desktop ativo.

Próximo sugerido: ler o peso original dos itens e definir um limite explícito
de carga para o perfil de teste. GPT-6.1 Sol, esforço Alto (High).
