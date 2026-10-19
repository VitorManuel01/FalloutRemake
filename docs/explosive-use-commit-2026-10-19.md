# Marco 52 — commit da proposta com revalidação

Propostas agora guardam GUID de uso, token da sessão e revisão do inventário de
origem. `WorldSession.CommitExplosiveUse` confere esses valores, a pilha atual,
quantidade um, perfil/hash do item, posição real, navegação e parâmetros do
temporizador antes de criar a alocação. Não confia na quantidade antiga da janela.

Sucesso retira somente uma unidade da bolsa e registra local/prazo na mesma
operação lógica; a quantidade restante na origem não muda. A alocação já é
persistida pelo v3. Repetir a proposta ou aplicar outra proposta antiga da mesma
pilha é recusado. Reset incrementa a revisão; carregar cria outro token de sessão,
inclusive quando os dados do checkpoint forem idênticos.

O método aceita origem em outra área visitada e posicionamento na área atual.
Movimento, posição alterada, saída, geometria bloqueada, protótipo modificado ou
parâmetros inválidos não alteram a sessão. Não concede XP, não cria um objeto
visual, não avança tempo e não aplica dano/barreira. A interface permanece sem
botão de ativação; é uma operação de domínio para os próximos incrementos.

Validação: build sem avisos/erros e 188 grupos sintéticos. Novos casos cobrem
commit/round-trip, concorrência de propostas, reaplicação, reset/load, retirada e
devolução intermediárias, ator em movimento, fonte e geometria modificadas,
perfil forjado e origem em área inativa. O relatório `--item-use-checkpoint-report`
usa o commit público na fixture real de dados e confere recusa de reaplicação
sem alteração do checkpoint. Dados fonte permanecem intactos.

Próximo: avançar somente os ticks lógicos dos eventos pendentes, preservando
seu prazo ao carregar. Efeitos e interface continuam separados até serem
validados. GPT-6.1 Sol, esforço Alto.
