

# Siegbedingungen

`win=bed1,bed2,...`

Bedingung | Beispiel | Bedeutung
-|-
`CatAt x y` | `CatAt 3 4` |Die Katze muss sich an den angegebenen Koordinaten befinden
`Val x y val` | `Val 3 4 Puschel` | In dem Feld muss sich der Wert `val` befinden
`MiceInBowls`|`MiceInBowls` | Alle Mäuse müssen in Näpfen sein
`CC col pat count` oder `ColorCount` |`win=CC Blue Diamond 16, CC None None 104` | Das Muster `pat` mit der Farbe `col` muss `count` oft vorkommen
`Nums x1 y1 v1 x2 y2 v2 ... xn yn vn` | `Nums 4 1 3 4 2 5 4 3 2 4 4 4 4 5 1` | Auf den Feldern  `(xi,xi)` müssen sich die Zahlen `vi` befinden







