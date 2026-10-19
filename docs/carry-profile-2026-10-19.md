# Marco 33 — capacidade de carga do perfil de teste

Branch `codex/carry-weight-profile`, base `dc52e46`.

O perfil de teste usa Força 5 e capacidade 150 lb, conforme `25 * Força + 25`
em CE `stat.cc:571`. O peso carregado soma as pilhas de todas as áreas, inclusive
inativas. Coletar confere a quantidade inteira antes de transferir; ultrapassar
o limite recusa a operação sem alterar bolsa ou recipiente. Devolver libera carga.

`CarryWeight` usa o peso base PRO, ajusta as quatro armaduras motorizadas conforme
CE `item.cc:757` e retorna zero para o flag de item oculto do protótipo. O domínio
de coleta continua recusando armas naturais/itens ocultos. Armas com munição
carregada ou estado de munição desconhecido ainda não têm cálculo suportado:
o peso fica indisponível e a coleta é recusada, sem inventar uma estimativa.
Sem perks, mudanças de atributos ou peso de recipientes aninhados.

Checkpoint mantém o formato v2. Pesos são recalculados dos protótipos validados;
não são campos editáveis no arquivo. Um checkpoint antigo acima do novo limite
continua recuperável, permitindo devolver itens, mas não novas retiradas. O mesmo
vale para peso antigo desconhecido. O limite é de novas coletas, sem regra nova
de movimento ou descarte automático.

Validação: compilação sem avisos/erros, **144 grupos sintéticos aprovados**.
Sete novos grupos conferem fórmula/atributo, limite exato, excesso atômico,
devolução, legado acima do limite, zero/quantidades grandes/overflow, peso
desconhecido, ajustes do CE e soma entre áreas após checkpoint. Integração Godot
sem janela também confere o item real de 1 lb no exterior e capacidade 150,
com todos os fluxos anteriores de coleta/devolução preservados.

Logs `.local/carry-build.log`, `.local/carry-synthetic.log`, `.local/carry-preview.log`.
Próximo: exibir carga e peso das pilhas nas janelas J/B. GPT-6.1 Sol, esforço Alto.
