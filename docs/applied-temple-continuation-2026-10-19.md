# Ensaio real: aplicar a barreira, coletar na elevação 2 e voltar

`--barrier-commit-report` reutiliza o percurso real até o explosivo do pote 358:
182 passos, portas originais e sucesso controlado no arrombamento/temporizador.
Depois do vencimento aplica o efeito validado da barreira. A unidade 364/PID 85
fica consumida; a substituição possui GUID próprio e não altera os bytes MAP.

A passagem abre 14322/elevação 1 e expande a região a 1258 hexes. O ensaio caminha
ao antigo bloqueador, salva e carrega nessa posição, atravessa a saída original
até 23914/elevação 2 e verifica os 974 hexes alcançáveis a partir da chegada.

Coleta de fato as duas unidades do baú 376: Healing Powder ID 126/PID 273 e
Antidote ID 100/PID 49. A bolsa fica com duas unidades/peso base 2. O baú 100 com
script permanece vazio e com `ScriptRequired`; o ensaio não o habilita.

Salva/recarrega a coleta, retorna pela passagem original para 14722/elevação 1,
caminha novamente a 14322 e faz outro round-trip. As três retomadas conservam
posição, GUID da substituição, consumo, inventário e os 25 XP do arrombamento.
O efeito em si não concede XP. São 142 passos adicionais, 324 no total.
Repetir o efeito é recusado sem mudar o checkpoint.

```powershell
dotnet run --project tools/TempleInspector -- --data-dir 'D:\Games\fallout2\Fallout 2 English' --barrier-commit-report
```

O relatório padrão `--barrier-effect-report` continua sem mutações. Apenas a
opção nova aplica o recorte de cenário, numa sessão de auditoria própria. Não há
botão de ativação, relógio de parede, dano a personagens ou combate geral.
Ambos os relatórios exigem reconstrução byte a byte das fontes. O novo relatório
entrou na bateria, agora com treze etapas e nove relatórios reais.

Próximo: refletir a sobreposição na prévia 3D e verificar os mesmos checkpoints
e passagens no Godot isolado. GPT-6.1 Sol, esforço Alto.
