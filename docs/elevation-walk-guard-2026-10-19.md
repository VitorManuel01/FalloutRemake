# Marco 47 — troca de elevação durante caminhada

O seletor de elevação podia reconstruir a cena durante um passo interpolado e
descartar a caminhada. Agora a solicitação do usuário mantém o percurso e o
progresso, restaura a opção selecionada e pede terminar ou parar com Esc antes
de mudar a elevação. Selecionar a mesma elevação não reconstrói a cena.

Entrada [F] também respeita essa regra se precisar mudar a elevação exibida.
Quando já exibe a mesma elevação, continua podendo enquadrar a entrada durante
a caminhada. Viagens, restauração de checkpoint e reset usam a reconstrução
interna existente, após suas próprias verificações.

Validação Godot: seletor real da HUD durante passo fracionário, seleção visual
restaurada, mesma instância de walker, percurso/posição contínuos e caminhada
que prossegue. Após Esc e conclusão do passo, inspeção e retorno à entrada
funcionam sem teleporte. A sequência real de viagem confere F durante uma
caminhada na elevação 1. Portas e bytes fonte permanecem intactos.

Build sem avisos/erros e integração sem janela com saída 0.
`ELEVATION_WALK_GUARD_OK` identifica o novo teste; as demais integrações passaram.
Validação gráfica pelo runner isolado, mantendo o desktop ativo intacto.

Próximo: revisão final da bateria e registros locais atualizados.
GPT-6.1 Sol, esforço Alto.
