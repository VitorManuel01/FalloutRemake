# Marco 43 — condição da barreira e substituição

`aibkdor.int` (2470 bytes, SHA-256 `11749956…426EA`) vincula o cenário ID 44,
offset 303324, hex 14322, elevação 1. Em `damage_p_proc`, metarule 49 recebe
`target_obj` e seu resultado é comparado com 6 (dano explosivo). Se a condição
for falsa, IF salta para 2358. Isso não restringe o gatilho exclusivamente ao
Plastic Explosives; a origem precisa ter tipo de dano explosivo válido no CE.

Quando verdadeira, a sequência cria PID 33555365 no mesmo tile/elevação do objeto
e destrói `self`. O push 1 seguido de opcode 0x8046 é negação: produz SID -1
para `create_object_sid`, sem mudar a elevação. Não há concessão de XP no trecho.

O protótipo de substituição é `proto/scenery/00000934.pro`, 45 bytes, SHA-256
`F374D40B…6A730B`, FID 33555376, subtipo 5, sem script. Suas flags incluem
NoBlock. O experimento de geometria, sem modificar a sessão, oculta apenas a
barreira original e considera a substituição não bloqueadora: a região da
entrada 29097 passa de 1245 a 1258 hexes e alcança seis saídas para a elevação 2.
Isso explica a fronteira encontrada no marco 42, sem habilitar destruição no jogo.

```powershell
dotnet run --project tools/TempleInspector -- --data-dir 'D:\Games\fallout2\Fallout 2 English' --temple-barrier-report
```

O relatório exige hashes e a sequência exata de 19 instruções críticas,
confere o novo PRO e reconstrói o MAP. CE local no commit já fixado:
`interpreter.cc:812` (IF), `interpreter.h` (NEGATE), `interpreter_extra.cc`
(metarule, create/destroy), `object.cc:890` (flags iniciais).
`scripts.cc:2879` transmite dano a scripts na mesma elevação e dentro do raio,
usando a explosão como target. O raio/dados do explosivo e seu ciclo de uso
ainda precisam de validação antes de um adaptador jogável.

Próximo: validar distância/raio e consumo do explosivo; depois persistência da
barreira e identidade do cenário criado. GPT-6.1 Sol, esforço Alto.
