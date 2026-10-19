# Marco 40 — arte original do inventário

O campo `InventoryFid` vem do offset 52 do PRO de item, separado do FID usado
no mundo. O CE confirma isso em `protoRead` e `itemGetInventoryFid`.
O catálogo usa tipo 7 e índice de 12 bits, sem renumerar as linhas de
`art/inven/inven.lst`. FID -1 significa ausência de ícone.

O decoder aceita FRM v4 com um frame completo, até 512×512, sem blocos de dados
por direção. Retém deslocamentos assinados, mas a apresentação de inventário
usa pixels centrados, como `artRender(frame 0, direção 0)`. Índice zero é
transparente; `color.pal` fornece cores de seis bits. O piso usa a mesma rotina
de paleta, preservando suas validações anteriores. Animação e FIDs com campos
extras ficam sem suporte explícito.

Nos dados Epic, os cinco ícones referenciados por protótipos do templo foram
decodificados: Spear 159×39, Antidote 90×62, Plastic Explosives 93×63,
Healing Powder 47×63 e Temple Key 86×54. A chave é referência de protótipo,
não um novo item criado na bolsa. Os quatro tipos de conteúdo de recipientes
usam seus respectivos ícones.

Validação: build sem avisos/erros, 156 grupos sintéticos aprovados, incluindo
limites, truncamento, direções, cores/transparência, offsets e IDs/cache.
O relatório real confere hashes, dimensões e reconstrução byte a byte do MAP:

```powershell
dotnet run --project tools/TempleInspector -- --data-dir 'D:\Games\fallout2\Fallout 2 English' --item-art-report
```

Não há extração de assets para o Git. Próximo: apresentar os ícones na coleta
e bolsa, com fallback quando a arte não estiver disponível. GPT-6.1 Sol, esforço Alto.
