# Marco 42 — auditoria de coleta entre elevações

O cenário CLI percorre a geometria real, abre a primeira porta e usa sucesso
determinístico no adaptador de Lockpick já existente para abrir a segunda.
Recolhe os conteúdos estáticos alcançáveis e atravessa somente saídas suportadas,
sem teleporte ou alteração de dados fonte. Não simula combate, armadilhas ou
execução geral de scripts; 527 passos é o percurso deste cenário, não uma rota ótima.

Resultado nos dados Epic: cinco unidades/11 lb, incluindo Spear, dois Healing
Powder, Plastic Explosives e Antidote. A sessão chega à elevação 1, conserva
25 XP e restaura bolsa/portas/posição por checkpoint byte a byte equivalente.
O baú ID 376 da elevação 2 ainda tem suas duas unidades. O baú com script
ID 100 permanece vazio e sem adaptador.

O cenário encontra uma barreira antes da elevação 2. O objeto ID 44,
offset 303324, PID 33555364, hex 14322, subtipo de cenário 5 usa `aibkdor.int`.
Ele não é uma porta do subtipo 0; portanto não deve receber Toggle/Lockpick
por analogia com as duas portas anteriores. O relatório enumera bloqueadores
com script alcançáveis para orientar o próximo estudo.

```powershell
dotnet run --project tools/TempleInspector -- --data-dir 'D:\Games\fallout2\Fallout 2 English' --temple-collection-report
```

Validação: cenário real com checkpoint completo e reconstrução do MAP, sem
criar/usar o slot do usuário nem abrir janelas. O comando valida o que já é
suportado e registra a fronteira; não declara o Temple of Trials completo.
Próximo: mapear `aibkdor.int` e as condições de explosão antes de implementar
uso de explosivos. GPT-6.1 Sol, esforço Alto.
