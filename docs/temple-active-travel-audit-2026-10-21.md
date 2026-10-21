# Saída ativa: viagens, bloqueios e retomada

A auditoria de abertura da barreira agora inclui `WORLD_TEMPLE_SPEECH_AUDIT_OK`:
parte dos dois itens realmente recolhidos e dos 25 XP de arrombamento, aceita o
ramo Speech em um mundo separado e percorre os 91 passos com o bloqueador ativo.
Confere cinco restaurações completas, uma espera parcial, ida/volta pela escada
original e preservação de itens, usos, substituição de cenário e mundo chamador.

O NPC parado no hex 19728 pode bloquear o corredor de retorno. O fixture precisa
atravessar a porta com o jogador antes dessa parada para alcançar a escada.
Esse caso foi observado durante a auditoria; nenhum bloqueador foi removido
para obter passagem. Fora da elevação 2, passos e tempo do NPC são recusados sem
mudança. Um grupo sintético adicional verifica viagem externa, gravação,
carregamento e retorno, elevando a suíte a **295 grupos**.

A integração gráfica cobre um trecho parcialmente obstruído: os passos válidos
anteriores permanecem aplicados e a malha acompanha o último hex alcançado; o
NPC para antes do jogador. A revelação dos controles enquadra o título da seção
depois do layout, evitando mostrar somente seus últimos botões em tela menor.

Validações: relatório real `.local/world-temple-speech-audit.json`, testes em
`.local/active-speech-travel-tests.log` e captura isolada 1024×768 em
`.local/active-speech-travel-isolated.json`, `Passed=true`, logs limpos e entrada
`Default` preservada. Build sem avisos/erros.
