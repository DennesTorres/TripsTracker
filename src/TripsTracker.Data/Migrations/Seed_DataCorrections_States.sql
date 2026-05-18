-- Restore Brazilian state assignments
-- These were lost when VisitedStates table was replaced by a VIEW derived from Places.StateAbbr.
-- The original Places seed data had no StateAbbr — it was stored in a separate VisitedStates table.

UPDATE Places SET StateAbbr = 'AM' WHERE Id IN (59, 60);         -- Manaus, Tefé
UPDATE Places SET StateAbbr = 'RO' WHERE Id IN (61);             -- Porto Velho
UPDATE Places SET StateAbbr = 'DF' WHERE Id IN (62);             -- Brasília
UPDATE Places SET StateAbbr = 'GO' WHERE Id IN (63, 64, 65);     -- Minaçu, Alto Paraíso, Chapada dos Veadeiros
UPDATE Places SET StateAbbr = 'CE' WHERE Id IN (66, 67, 68, 69); -- Fortaleza, Jericoacoara, Canoa Quebrada, Tatajuba
UPDATE Places SET StateAbbr = 'RN' WHERE Id IN (70);             -- Natal
UPDATE Places SET StateAbbr = 'AL' WHERE Id IN (71, 72, 73, 74, 75, 76); -- Maceió, São Miguel dos Milagres, Barra de Santo Antônio, Porto Calvo, Piranhas, Cânion do Xingó
UPDATE Places SET StateAbbr = 'SE' WHERE Id IN (77);             -- Sergipe — Xingó border
UPDATE Places SET StateAbbr = 'BA' WHERE Id IN (78, 79, 80, 81, 82); -- Lençóis, Chapada Diamantina, Barra do Rocha, Ubatã, Ipiaú
UPDATE Places SET StateAbbr = 'MG' WHERE Id IN (83, 84, 85, 86, 87, 88, 89, 90, 91, 92); -- Belo Horizonte, Sete Lagoas, Ouro Preto, Mariana, Pedro Leopoldo, Juiz de Fora, Ituiutaba, Patos de Minas, Rio Novo, São Lourenço
UPDATE Places SET StateAbbr = 'RJ' WHERE Id IN (93, 94, 95, 96, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116, 117, 118, 119, 120, 121, 122, 123, 124, 125, 126, 127, 128, 129, 130);
  -- Resende, Penedo, Itatiaia, Engenheiro Passos, Rio de Janeiro, Niterói, Angra dos Reis, Ilha Grande, Búzios, Itaperuna,
  -- Petrópolis, Três Rios, Paraty, Mangaratiba, Cabo Frio, Arraial do Cabo, São Pedro da Aldeia, Macaé, Nova Friburgo,
  -- Duque de Caxias, Nova Iguaçu, São Gonçalo, Maricá, Itaboraí, Queimados, Belford Roxo, Nilópolis, São João de Meriti,
  -- Paracambi, Miguel Pereira, Piraí
UPDATE Places SET StateAbbr = 'ES' WHERE Id IN (99, 100, 101, 102, 103); -- Vila Velha, Guarapari, Vitória, Linhares, Bom Jesus do Norte
UPDATE Places SET StateAbbr = 'SP' WHERE Id IN (131, 132, 133, 134, 135, 136, 137, 138, 139, 140, 141, 142, 143, 144, 145, 146, 147);
  -- São Paulo, Osasco, Campinas, Praia Grande, Santos, Guarulhos, Barueri, Boituva, Campos do Jordão,
  -- São José dos Campos, Paranapiacaba, Jundiaí, Rio Claro, Limeira, São Carlos, São Roque, Ubatuba
UPDATE Places SET StateAbbr = 'PR' WHERE Id IN (148);            -- Foz do Iguaçu
UPDATE Places SET StateAbbr = 'SC' WHERE Id IN (149);            -- Porto União
UPDATE Places SET StateAbbr = 'RS' WHERE Id IN (150, 151, 152, 153, 154, 155, 156, 157); -- Gramado, Canela, Três Coroas, Nova Petrópolis, Bento Gonçalves, Garibaldi, Carlos Barbosa, Caxias do Sul
UPDATE Places SET StateAbbr = 'MS' WHERE Id IN (158, 159);       -- Campo Grande, Bonito
