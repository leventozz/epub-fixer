# Lattice OCR Report

## Summary

- Apply: 60
- Review: 20
- Leave: 483
- Max visited states per region: 1713

## Reason Histogram

| Reason | Count |
| --- | ---: |
| OriginalTokenIsValid | 418 |
| Accepted | 60 |
| NoChange | 40 |
| MarginTooSmall | 20 |
| CostAboveThreshold | 12 |
| TooManyOrdinaryEdits | 12 |
| NoPath | 1 |

## Decisions

| Region | Verdict | Replacement | Reasons |
| --- | --- | --- | --- |
| `1952-1957` | Leave | `` | NoChange |
| `(Yukarı` | Leave | `` | NoChange |
| `(Yukarı` | Leave | `` | NoChange |
| `öldü.Kitap-lık` | Leave | `` | OriginalTokenIsValid |
| `(Fischer` | Leave | `` | NoChange |
| `(Adımlar)` | Leave | `` | OriginalTokenIsValid |
| `kitapları:Odun` | Leave | `` | NoChange |
| `(1999)Bitik` | Leave | `` | NoChange |
| `(2000)Eski` | Leave | `` | NoChange |
| `(2002)Ses` | Leave | `` | NoChange |
| `(2003)Yok` | Leave | `` | NoChange |
| `(2005)Don` | Leave | `` | NoChange |
| `(2006)Beton` | Leave | `` | NoChange |
| `(2007)Yürümek` | Leave | `` | NoChange |
| `(2009)Ödüllerim` | Leave | `` | NoChange |
| `(2010)Düzelti` | Apply | `Ödüllerle` | Accepted |
| `(2011)THOMAS` | Leave | `` | NoChange |
| `ÖfkeÇeviren:Sezer` | Leave | `` | NoChange |
| `-343Odun` | Leave | `` | OriginalTokenIsValid |
| `Holzfallen-Eine` | Review | `` | MarginTooSmall |
| `(0` | Leave | `` | OriginalTokenIsValid |
| `1` | Apply | `YKYdedi` | Accepted |
| `(O` | Leave | `` | OriginalTokenIsValid |
| `(pbx)` | Apply | `oo` | Accepted |
| `(O` | Leave | `` | OriginalTokenIsValid |
| `sosyete-si` | Leave | `` | OriginalTokenIsValid |
| `Cadde-si'ne` | Apply | `Caddesi'ne` | Accepted |
| `tecritin-den` | Leave | `` | OriginalTokenIsValid |
| `Auersber-ger` | Leave | `` | OriginalTokenIsValid |
| `Auersberger-ler'le` | Leave | `` | OriginalTokenIsValid |
| `Pa-olo` | Leave | `` | OriginalTokenIsValid |
| `kalma-yıp` | Apply | `kalmayıp` | Accepted |
| `Soka-ğı'na` | Apply | `Sokağı'na` | Accepted |
| `Soka-ğı'na` | Apply | `Sokağı'na` | Accepted |
| `(son` | Leave | `` | OriginalTokenIsValid |
| `Auersber-ger` | Leave | `` | OriginalTokenIsValid |
| `Za-al'dan` | Apply | `Zaal'da` | Accepted |
| `çiz-meli` | Leave | `` | OriginalTokenIsValid |
| `Ala-nı'na` | Apply | `Alanı'na` | Accepted |
| `(Alm.)` | Review | `` | MarginTooSmall |
| `(Yay` | Leave | `` | OriginalTokenIsValid |
| `kuru-munu` | Leave | `` | OriginalTokenIsValid |
| `mimci-lcrin` | Leave | `` | OriginalTokenIsValid |
| `yorgun-luktan` | Leave | `` | OriginalTokenIsValid |
| `^iddetli` | Leave | `` | OriginalTokenIsValid |
| `ilgi-1 iydi` | Leave | `` | OriginalTokenIsValid |
| `Birçokkez,Joana'nın` | Leave | `` | NoChange |
| `:;;aşılacak` | Leave | `` | TooManyOrdinaryEdits |
| `Viya-na'dan` | Leave | `` | OriginalTokenIsValid |
| `kü-^:ük` | Apply | `küçük` | Accepted |
| `Au-('rsberger'in` | Leave | `` | OriginalTokenIsValid |
| `Soka-f^ı'nda` | Leave | `` | OriginalTokenIsValid |
| `<mcak` | Leave | `` | OriginalTokenIsValid |
| `Soka-ğı'na` | Apply | `Sokağı'na` | Accepted |
| `ı` | Leave | `` | OriginalTokenIsValid |
| `ce-ı ı` | Leave | `` | OriginalTokenIsValid |
| `<ı ze` | Leave | `` | OriginalTokenIsValid |
| `fo-ana` | Leave | `` | OriginalTokenIsValid |
| `anla-ınıyormuşum` | Leave | `` | OriginalTokenIsValid |
| `deği-^ik` | Leave | `` | OriginalTokenIsValid |
| `Auersber-ger` | Leave | `` | OriginalTokenIsValid |
| `hipermetroplu-ğun` | Leave | `` | CostAboveThreshold |
| `Auersberger-ler'in` | Leave | `` | OriginalTokenIsValid |
| `Kilb-li` | Leave | `` | OriginalTokenIsValid |
| `ger-^` | Leave | `` | OriginalTokenIsValid |
| `angajma-ııa` | Leave | `` | CostAboveThreshold |
| `Ala-nı'nda` | Apply | `Alanı'nda` | Accepted |
| `ı ı ygun` | Leave | `` | OriginalTokenIsValid |
| `Viya-ııa'da` | Leave | `` | OriginalTokenIsValid |
| `(ki` | Leave | `` | NoChange |
| `(Meksikalı` | Leave | `` | NoChange |
| `Jo-;ına` | Leave | `` | OriginalTokenIsValid |
| `l<ilb'de` | Leave | `` | OriginalTokenIsValid |
| `adlandırı-lan` | Leave | `` | OriginalTokenIsValid |
| `;ışmışmış` | Leave | `` | NoChange |
| `i` | Review | `` | MarginTooSmall |
| `çıkart-ınıştı` | Leave | `` | OriginalTokenIsValid |
| `devle-L` | Leave | `` | OriginalTokenIsValid |
| `yolcu-1` | Leave | `` | OriginalTokenIsValid |
| `dü-^ündüm` | Leave | `` | OriginalTokenIsValid |
| `Viya-na'da` | Leave | `` | OriginalTokenIsValid |
| `pe-^inden` | Leave | `` | OriginalTokenIsValid |
| `Viya-na'da` | Leave | `` | OriginalTokenIsValid |
| `Jo-ana` | Leave | `` | OriginalTokenIsValid |
| `ya-ııi` | Leave | `` | OriginalTokenIsValid |
| `;li ndüm` | Leave | `` | OriginalTokenIsValid |
| `ı ılasılıkla` | Leave | `` | OriginalTokenIsValid |
| `c;,ığlardı` | Leave | `` | OriginalTokenIsValid |
| `ı ıyuncuları` | Leave | `` | OriginalTokenIsValid |
| `ı ıyuncu` | Leave | `` | OriginalTokenIsValid |
| `bu-ı` | Leave | `` | OriginalTokenIsValid |
| `<ıya` | Leave | `` | OriginalTokenIsValid |
| `ak-:;nm` | Leave | `` | OriginalTokenIsValid |
| `ı` | Leave | `` | OriginalTokenIsValid |
| `kada-n` | Leave | `` | OriginalTokenIsValid |
| `;rınatsal` | Leave | `` | NoChange |
| `Örde-ği'nde` | Apply | `Ördeği'nde` | Accepted |
| `Örde-ği'nin` | Apply | `Ördeği'nin` | Accepted |
| `(belirtmeliyim` | Leave | `` | NoChange |
| `üze-ıi ııde` | Leave | `` | CostAboveThreshold |
| `arka-d. ı` | Leave | `` | OriginalTokenIsValid |
| `za-ııı<ın` | Leave | `` | OriginalTokenIsValid |
| `bul-ııı<ılarını` | Leave | `` | OriginalTokenIsValid |
| `tabu-ı.ı` | Leave | `` | OriginalTokenIsValid |
| `ıı<ıda` | Apply | `yılda` | Accepted |
| `cese-< 1` | Leave | `` | OriginalTokenIsValid |
| `Gu-1. ı` | Leave | `` | OriginalTokenIsValid |
| `elleri-ıı i` | Leave | `` | OriginalTokenIsValid |
| `ı ııa'nın` | Leave | `` | OriginalTokenIsValid |
| `intiha-ı ından` | Leave | `` | OriginalTokenIsValid |
| `ı ına` | Leave | `` | OriginalTokenIsValid |
| `ı ııandığını` | Leave | `` | OriginalTokenIsValid |
| `gu-1.ı` | Leave | `` | OriginalTokenIsValid |
| `sıçrat-ı ıı ıştı` | Leave | `` | OriginalTokenIsValid |
| `ı` | Leave | `` | OriginalTokenIsValid |
| `ı` | Leave | `` | OriginalTokenIsValid |
| `,ı` | Leave | `` | OriginalTokenIsValid |
| `ı ı` | Leave | `` | OriginalTokenIsValid |
| `oldu-ı` | Leave | `` | OriginalTokenIsValid |
| `,ıı` | Leave | `` | OriginalTokenIsValid |
| `ye-ı 1 i` | Leave | `` | OriginalTokenIsValid |
| `1` | Review | `` | MarginTooSmall |
| `y;ıvaş` | Leave | `` | OriginalTokenIsValid |
| `sa-lıip` | Leave | `` | OriginalTokenIsValid |
| `><ına'nın` | Review | `` | MarginTooSmall |
| `kok-1 ıı,;;>,unu` | Leave | `` | CostAboveThreshold |
| `1 ıi` | Leave | `` | OriginalTokenIsValid |
| `cena-1'.!'Sİ` | Leave | `` | OriginalTokenIsValid |
| `l ııgiliz` | Leave | `` | OriginalTokenIsValid |
| `unu-1 ı ı` | Leave | `` | OriginalTokenIsValid |
| `iç-lı` | Leave | `` | OriginalTokenIsValid |
| `ol-ı` | Leave | `` | OriginalTokenIsValid |
| `ı ı;ıklaşıyorlar` | Leave | `` | OriginalTokenIsValid |
| `ı ı` | Leave | `` | OriginalTokenIsValid |
| `koltu-)` | Leave | `` | TooManyOrdinaryEdits |
| `,tında` | Leave | `` | TooManyOrdinaryEdits |
| `gitti-)` | Leave | `` | OriginalTokenIsValid |
| `i ınizde` | Leave | `` | OriginalTokenIsValid |
| `kah-w` | Leave | `` | OriginalTokenIsValid |
| `Aka-ılı` | Leave | `` | OriginalTokenIsValid |
| `l ı ı` | Leave | `` | OriginalTokenIsValid |
| `se-1. ı ına` | Leave | `` | OriginalTokenIsValid |
| `bü-viik` | Leave | `` | OriginalTokenIsValid |
| `alkışla-ıı;ın` | Leave | `` | OriginalTokenIsValid |
| `l,ın` | Leave | `` | OriginalTokenIsValid |
| `:;,ıat` | Leave | `` | OriginalTokenIsValid |
| `;entz` | Apply | `Gentz` | Accepted |
| `ı` | Leave | `` | OriginalTokenIsValid |
| `sa-1111tsal` | Leave | `` | OriginalTokenIsValid |
| `ı ıluz` | Leave | `` | OriginalTokenIsValid |
| `1 nı` | Leave | `` | OriginalTokenIsValid |
| `ı` | Leave | `` | OriginalTokenIsValid |
| `ı` | Leave | `` | OriginalTokenIsValid |
| `ı ıluz` | Leave | `` | OriginalTokenIsValid |
| `i` | Apply | `gerçek` | Accepted |
| `,erçek` | Apply | `gerçek` | Accepted |
| `ken-dimi` | Leave | `` | OriginalTokenIsValid |
| `Auersberger-ler'le` | Leave | `` | OriginalTokenIsValid |
| `;itmem` | Review | `` | MarginTooSmall |
| `ak-:,;,un` | Leave | `` | OriginalTokenIsValid |
| `,İtmeyeceğim` | Leave | `` | OriginalTokenIsValid |
| `törenin-ılc` | Leave | `` | OriginalTokenIsValid |
| `gel-ı` | Leave | `` | OriginalTokenIsValid |
| `doğal-( l ır` | Leave | `` | OriginalTokenIsValid |
| `:iindüm` | Leave | `` | OriginalTokenIsValid |
| `çalıştı-ı` | Leave | `` | OriginalTokenIsValid |
| `,ıın` | Leave | `` | OriginalTokenIsValid |
| `1` | Leave | `` | OriginalTokenIsValid |
| `>u` | Leave | `` | OriginalTokenIsValid |
| `l ıcr` | Leave | `` | OriginalTokenIsValid |
| `:i` | Leave | `` | OriginalTokenIsValid |
| `y;ılanlar` | Leave | `` | OriginalTokenIsValid |
| `bildi-ı` | Leave | `` | OriginalTokenIsValid |
| `,in` | Leave | `` | OriginalTokenIsValid |
| `hak-k ında` | Leave | `` | OriginalTokenIsValid |
| `ı ın lara` | Leave | `` | OriginalTokenIsValid |
| `ko-ınıkluktan` | Leave | `` | OriginalTokenIsValid |
| `ı le` | Leave | `` | OriginalTokenIsValid |
| `is-i<'ksizlik` | Leave | `` | OriginalTokenIsValid |
| `p;ıltomu` | Leave | `` | OriginalTokenIsValid |
| `So-k;ığı'na` | Leave | `` | OriginalTokenIsValid |
| `So-k;ığı'na` | Leave | `` | OriginalTokenIsValid |
| `gü-1 iinç` | Leave | `` | CostAboveThreshold |
| `;ofcağı'ndaki` | Leave | `` | NoChange |
| `:uın` | Leave | `` | OriginalTokenIsValid |
| `on-lurm` | Leave | `` | OriginalTokenIsValid |
| `gir-ılinı` | Leave | `` | OriginalTokenIsValid |
| `:ey` | Leave | `` | OriginalTokenIsValid |
| `Soka-ğı'nda` | Apply | `Sokağı'nda` | Accepted |
| `ı ı nun` | Leave | `` | OriginalTokenIsValid |
| `gele-11` | Leave | `` | CostAboveThreshold |
| `i:;;ilere` | Leave | `` | OriginalTokenIsValid |
| `tiksinti-v ı` | Leave | `` | OriginalTokenIsValid |
| `ı ı` | Leave | `` | OriginalTokenIsValid |
| `ı ı.lenimini` | Leave | `` | OriginalTokenIsValid |
| `Auers-lıerger` | Apply | `berger` | Accepted |
| `yemeği-ı ı` | Leave | `` | OriginalTokenIsValid |
| `1 lurg` | Review | `` | MarginTooSmall |
| `ı kşam` | Leave | `` | OriginalTokenIsValid |
| `r;ıhatıma` | Leave | `` | OriginalTokenIsValid |
| `ı.ı^renç` | Leave | `` | OriginalTokenIsValid |
| `;atta` | Leave | `` | OriginalTokenIsValid |
| `bozulmuş-1.ırdı` | Leave | `` | OriginalTokenIsValid |
| `diyebilir-ll'rdi` | Leave | `` | OriginalTokenIsValid |
| `ı ınlardan` | Leave | `` | OriginalTokenIsValid |
| `1 ıu` | Leave | `` | OriginalTokenIsValid |
| `et-ıııiş` | Leave | `` | OriginalTokenIsValid |
| `ı ıluz` | Leave | `` | OriginalTokenIsValid |
| `çıkmış-l ım` | Leave | `` | OriginalTokenIsValid |
| `gö-1'. ümden` | Leave | `` | OriginalTokenIsValid |
| `l;ımamen` | Leave | `` | OriginalTokenIsValid |
| `,uz` | Leave | `` | OriginalTokenIsValid |
| `ak-’jnm` | Leave | `` | OriginalTokenIsValid |
| `ı,ırarını` | Leave | `` | OriginalTokenIsValid |
| `ı ıç` | Leave | `` | OriginalTokenIsValid |
| `kendi-ıni` | Leave | `` | OriginalTokenIsValid |
| `dikkat-:;i zlikle` | Leave | `` | OriginalTokenIsValid |
| `1 ı iç` | Leave | `` | OriginalTokenIsValid |
| `:,ohbet` | Apply | `sohbet` | Accepted |
| `koli ukta` | Apply | `koltukta` | Accepted |
| `(le` | Leave | `` | OriginalTokenIsValid |
| `ı ızellikle` | Leave | `` | OriginalTokenIsValid |
| `(le` | Review | `` | MarginTooSmall |
| `ı ılduğu` | Leave | `` | OriginalTokenIsValid |
| `kendi-ıni` | Leave | `` | OriginalTokenIsValid |
| `Ce-lıimde` | Leave | `` | OriginalTokenIsValid |
| `( 1 iye` | Leave | `` | OriginalTokenIsValid |
| `--:<lbaha` | Leave | `` | OriginalTokenIsValid |
| `Anacadde-si'ni` | Review | `` | MarginTooSmall |
| `Ala-nı'nı` | Review | `` | MarginTooSmall |
| `Schwarzen-herg` | Review | `` | MarginTooSmall |
| `ge-^:cn` | Leave | `` | CostAboveThreshold |
| `Simmerin-ger` | Leave | `` | OriginalTokenIsValid |
| `yü-ıiimeye` | Apply | `yürümeye` | Accepted |
| `oldu,^unu` | Leave | `` | OriginalTokenIsValid |
| `1 ıerjer` | Apply | `berjer` | Accepted |
| `dü-^ündüm` | Leave | `` | OriginalTokenIsValid |
| `yeme-X1 i` | Leave | `` | OriginalTokenIsValid |
| `ilgi-IL'rini` | Leave | `` | OriginalTokenIsValid |
| `i1.;in` | Leave | `` | OriginalTokenIsValid |
| `sanat-t., ı` | Leave | `` | OriginalTokenIsValid |
| `ı rtık` | Leave | `` | OriginalTokenIsValid |
| `ay-ıı` | Leave | `` | OriginalTokenIsValid |
| `dönüştür-t lüğüm` | Leave | `` | OriginalTokenIsValid |
| `bunaltıcı,tıpkı` | Leave | `` | NoChange |
| `Viya-na'ya` | Leave | `` | OriginalTokenIsValid |
| `Dev-letleri'ne` | Leave | `` | OriginalTokenIsValid |
| `ge-ı` | Leave | `` | OriginalTokenIsValid |
| `ı` | Leave | `` | OriginalTokenIsValid |
| `l ıı` | Leave | `` | OriginalTokenIsValid |
| `l ıi` | Leave | `` | OriginalTokenIsValid |
| `l ıi` | Leave | `` | OriginalTokenIsValid |
| `sa-ııiyelik` | Leave | `` | OriginalTokenIsValid |
| `l ı i` | Leave | `` | OriginalTokenIsValid |
| `11<ı lyan` | Leave | `` | OriginalTokenIsValid |
| `gel-ı` | Leave | `` | OriginalTokenIsValid |
| `ı` | Leave | `` | OriginalTokenIsValid |
| `:rafen` | Leave | `` | OriginalTokenIsValid |
| `besteci-ıııiz` | Leave | `` | OriginalTokenIsValid |
| `kullanma-111111` | Leave | `` | OriginalTokenIsValid |
| `l ı` | Leave | `` | OriginalTokenIsValid |
| `elli-1 i` | Leave | `` | OriginalTokenIsValid |
| `1 ıi le` | Leave | `` | CostAboveThreshold |
| `(bu` | Leave | `` | NoChange |
| `et-ı ı ıl'yip` | Leave | `` | OriginalTokenIsValid |
| `daya-111` | Leave | `` | OriginalTokenIsValid |
| `birden-lıi` | Leave | `` | OriginalTokenIsValid |
| `ı. ıınamen` | Leave | `` | OriginalTokenIsValid |
| `otu-ı;ırak` | Leave | `` | OriginalTokenIsValid |
| `bur-juva` | Apply | `burjuva` | Accepted |
| `yurtdışı-na` | Leave | `` | OriginalTokenIsValid |
| `Viya-na'da` | Leave | `` | OriginalTokenIsValid |
| `düşün-diim` | Leave | `` | OriginalTokenIsValid |
| `güru-1ıunun` | Leave | `` | OriginalTokenIsValid |
| `üste-likde` | Leave | `` | OriginalTokenIsValid |
| `,Örünüm` | Apply | `Görünüm` | Accepted |
| `;ıfırları` | Review | `` | MarginTooSmall |
| `değil-ll i` | Leave | `` | OriginalTokenIsValid |
| `l.:ıkış` | Leave | `` | OriginalTokenIsValid |
| `c;anki` | Leave | `` | OriginalTokenIsValid |
| `c;anki` | Leave | `` | OriginalTokenIsValid |
| `iğrenç-1 ikleri` | Leave | `` | OriginalTokenIsValid |
| `(kendimle` | Apply | `ileri` | Accepted |
| `ı rdığım` | Leave | `` | OriginalTokenIsValid |
| `iğ-rmç` | Leave | `` | OriginalTokenIsValid |
| `c;öylemek` | Leave | `` | OriginalTokenIsValid |
| `ı` | Leave | `` | OriginalTokenIsValid |
| `gör-düm` | Leave | `` | OriginalTokenIsValid |
| `davran-dım` | Leave | `` | OriginalTokenIsValid |
| `(ya` | Leave | `` | NoChange |
| `(ya` | Leave | `` | NoChange |
| `(ya` | Leave | `` | NoChange |
| `(ya` | Leave | `` | NoChange |
| `(ya` | Leave | `` | NoChange |
| `Jo-,ma` | Leave | `` | OriginalTokenIsValid |
| `anla-^ılıyordu` | Leave | `` | OriginalTokenIsValid |
| `Konuşması-ıun` | Leave | `` | OriginalTokenIsValid |
| `zabı-tası` | Leave | `` | OriginalTokenIsValid |
| `(ya` | Leave | `` | NoChange |
| `Viya-na'dan` | Leave | `` | OriginalTokenIsValid |
| `<1tmak` | Leave | `` | TooManyOrdinaryEdits |
| `Jo-<ma'nın` | Leave | `` | OriginalTokenIsValid |
| `Auersber-ger` | Leave | `` | OriginalTokenIsValid |
| `(Bayan` | Apply | `ının` | Accepted |
| `Auersber-ger` | Leave | `` | OriginalTokenIsValid |
| `1` | Review | `` | MarginTooSmall |
| `:lcdal'ı` | Apply | `iledal'ı` | Accepted |
| `>izlik` | Leave | `` | OriginalTokenIsValid |
| `Au-ı` | Leave | `` | OriginalTokenIsValid |
| `küçümseyici-liği` | Leave | `` | TooManyOrdinaryEdits |
| `t:l'de` | Leave | `` | OriginalTokenIsValid |
| `Jo-ana` | Leave | `` | OriginalTokenIsValid |
| `Anacaddesi'ndekilı;ıreket` | Leave | `` | NoPath |
| `1 ıu` | Leave | `` | OriginalTokenIsValid |
| `gel-ıııeye` | Leave | `` | TooManyOrdinaryEdits |
| `ti-v<ıtrocular` | Leave | `` | OriginalTokenIsValid |
| `do-)',<ıl` | Leave | `` | OriginalTokenIsValid |
| `1 ıcri` | Leave | `` | CostAboveThreshold |
| `bildi-)',İtn` | Leave | `` | OriginalTokenIsValid |
| `et-ıııiş` | Leave | `` | OriginalTokenIsValid |
| `iı.:in` | Leave | `` | OriginalTokenIsValid |
| `1 ıl'rjer` | Leave | `` | TooManyOrdinaryEdits |
| `kanıtla-ıııak` | Leave | `` | OriginalTokenIsValid |
| `ı ına` | Leave | `` | OriginalTokenIsValid |
| `ı` | Leave | `` | OriginalTokenIsValid |
| `açıkla-ıııış` | Leave | `` | OriginalTokenIsValid |
| `ı 1 iye` | Leave | `` | OriginalTokenIsValid |
| `kazan-ıııadığı` | Leave | `` | OriginalTokenIsValid |
| `ı` | Leave | `` | OriginalTokenIsValid |
| `ı,ıman` | Leave | `` | OriginalTokenIsValid |
| `ı ığursuzluk` | Leave | `` | OriginalTokenIsValid |
| `vazgeç-ı` | Leave | `` | OriginalTokenIsValid |
| `l<alksburg'dan` | Leave | `` | OriginalTokenIsValid |
| `Jo-ana` | Leave | `` | OriginalTokenIsValid |
| `ger-<<` | Leave | `` | OriginalTokenIsValid |
| `1` | Leave | `` | OriginalTokenIsValid |
| `>ı ı` | Leave | `` | OriginalTokenIsValid |
| `ı` | Leave | `` | OriginalTokenIsValid |
| `,ibi` | Leave | `` | OriginalTokenIsValid |
| `göz-1<` | Leave | `` | OriginalTokenIsValid |
| `:;onra` | Leave | `` | OriginalTokenIsValid |
| `olma-<` | Leave | `` | OriginalTokenIsValid |
| `,iindüm` | Leave | `` | OriginalTokenIsValid |
| `l<ilb'de` | Leave | `` | OriginalTokenIsValid |
| `:;,matçısı` | Review | `` | MarginTooSmall |
| `zaman-1.ır` | Leave | `` | OriginalTokenIsValid |
| `za-ınanlar` | Apply | `zamanlar` | Accepted |
| `sanatçı-l<Hın` | Leave | `` | OriginalTokenIsValid |
| `Caddesi'nde-ki` | Leave | `` | OriginalTokenIsValid |
| `Ala-ııı'nda` | Apply | `Alanı'nda` | Accepted |
| `gider-<1` | Leave | `` | OriginalTokenIsValid |
| `,ıranan` | Leave | `` | OriginalTokenIsValid |
| `ressamlı-)^ından` | Leave | `` | OriginalTokenIsValid |
| `ı ı` | Leave | `` | OriginalTokenIsValid |
| `ı` | Leave | `` | OriginalTokenIsValid |
| `ı ılup` | Leave | `` | OriginalTokenIsValid |
| `l ıi ı` | Leave | `` | OriginalTokenIsValid |
| `oluvermiş-1 ı rn` | Leave | `` | OriginalTokenIsValid |
| `böy-lı` | Leave | `` | OriginalTokenIsValid |
| `ı. ı` | Leave | `` | OriginalTokenIsValid |
| `Se-lı;ıstian` | Leave | `` | OriginalTokenIsValid |
| `ı` | Leave | `` | OriginalTokenIsValid |
| `ı. ınımıştım` | Apply | `tanımıştım` | Accepted |
| `1 ıi` | Leave | `` | OriginalTokenIsValid |
| `l ıiiyle` | Leave | `` | OriginalTokenIsValid |
| `1` | Apply | `göster` | Accepted |
| `ı leyişle` | Leave | `` | OriginalTokenIsValid |
| `Ala-nı'nda` | Apply | `Alanı'nda` | Accepted |
| `ı` | Leave | `` | OriginalTokenIsValid |
| `bi-^:imde` | Apply | `biçimde` | Accepted |
| `Ala-nı'nda` | Apply | `Alanı'nda` | Accepted |
| `Sebas-tian` | Leave | `` | OriginalTokenIsValid |
| `Ödü-li.ı'nü` | Leave | `` | OriginalTokenIsValid |
| `lıL^r` | Leave | `` | OriginalTokenIsValid |
| `kL^ndisinden` | Leave | `` | OriginalTokenIsValid |
| `Jo-.ına` | Leave | `` | OriginalTokenIsValid |
| `ger-t cği` | Leave | `` | OriginalTokenIsValid |
| `son-r;ı` | Leave | `` | OriginalTokenIsValid |
| `ger-t'ekten` | Leave | `` | OriginalTokenIsValid |
| `1 ıaşarı` | Leave | `` | OriginalTokenIsValid |
| `dendi-);,i` | Leave | `` | OriginalTokenIsValid |
| `res-s;ım` | Leave | `` | OriginalTokenIsValid |
| `içti-g i` | Leave | `` | OriginalTokenIsValid |
| `ulaş-1 ırdı` | Leave | `` | OriginalTokenIsValid |
| `an-J;ımını` | Leave | `` | OriginalTokenIsValid |
| `nef-ret` | Leave | `` | OriginalTokenIsValid |
| `kal-ı nak` | Leave | `` | OriginalTokenIsValid |
| `kal-ı nadığı` | Leave | `` | OriginalTokenIsValid |
| `1` | Leave | `` | OriginalTokenIsValid |
| `>u` | Leave | `` | OriginalTokenIsValid |
| `c;okağı'nda` | Leave | `` | OriginalTokenIsValid |
| `edilme-:;i` | Leave | `` | OriginalTokenIsValid |
| `olmadı-)',1111` | Leave | `` | OriginalTokenIsValid |
| `1 ıunca` | Apply | `bunca` | Accepted |
| `çif-ı` | Leave | `` | OriginalTokenIsValid |
| `ken-d iıne` | Leave | `` | CostAboveThreshold |
| `duy-)',usallaştırıyoruz` | Leave | `` | OriginalTokenIsValid |
| `gitmeme-ı niz` | Leave | `` | OriginalTokenIsValid |
| `ı` | Leave | `` | OriginalTokenIsValid |
| `ı le` | Leave | `` | OriginalTokenIsValid |
| `budalalı-gım` | Leave | `` | OriginalTokenIsValid |
| `ı l` | Leave | `` | OriginalTokenIsValid |
| `^imdi` | Leave | `` | OriginalTokenIsValid |
| `^ey` | Leave | `` | OriginalTokenIsValid |
| `Auersberger-ler'in` | Leave | `` | OriginalTokenIsValid |
| `sanat-.^rıl` | Leave | `` | OriginalTokenIsValid |
| `ak-^am` | Leave | `` | OriginalTokenIsValid |
| `düşün-ı 1 i` | Leave | `` | OriginalTokenIsValid |
| `,Crçekten` | Apply | `Gerçekten` | Accepted |
| `ı:ayan` | Leave | `` | OriginalTokenIsValid |
| `aristokra-^;isi` | Leave | `` | OriginalTokenIsValid |
| `ı nnesi` | Leave | `` | OriginalTokenIsValid |
| `Ste-icrmarklı` | Leave | `` | OriginalTokenIsValid |
| `Au-ı` | Leave | `` | OriginalTokenIsValid |
| `^erefsiz` | Apply | `gereksiz` | Accepted |
| `yüzün-den` | Leave | `` | OriginalTokenIsValid |
| `Steier-ııı;ırk` | Leave | `` | OriginalTokenIsValid |
| `I,ırzı` | Leave | `` | OriginalTokenIsValid |
| `ı ı` | Leave | `` | OriginalTokenIsValid |
| `ı ı` | Leave | `` | OriginalTokenIsValid |
| `ı,ıvallı` | Leave | `` | OriginalTokenIsValid |
| `r,ığmen` | Leave | `` | OriginalTokenIsValid |
| `1 ıliyülü` | Leave | `` | OriginalTokenIsValid |
| `kol-1 ıı kta` | Leave | `` | TooManyOrdinaryEdits |
| `1 ıu` | Leave | `` | OriginalTokenIsValid |
| `ı ıtuz` | Leave | `` | OriginalTokenIsValid |
| `ı ılgu` | Leave | `` | OriginalTokenIsValid |
| `kazan-ınamış` | Leave | `` | OriginalTokenIsValid |
| `ı` | Leave | `` | OriginalTokenIsValid |
| `^;<ığlayabilirdi` | Leave | `` | OriginalTokenIsValid |
| `Au-ı` | Leave | `` | OriginalTokenIsValid |
| `bildi-gim` | Leave | `` | OriginalTokenIsValid |
| `i` | Leave | `` | CostAboveThreshold |
| `ı` | Leave | `` | OriginalTokenIsValid |
| `Viyanalı-ların` | Leave | `` | OriginalTokenIsValid |
| `Ma-ria` | Leave | `` | OriginalTokenIsValid |
| `ya-^amlarını` | Leave | `` | OriginalTokenIsValid |
| `de-gil` | Leave | `` | OriginalTokenIsValid |
| `yöne-1 ik` | Leave | `` | OriginalTokenIsValid |
| `ha-1.ırladıkları` | Leave | `` | CostAboveThreshold |
| `çullanmış-lardı` | Leave | `` | TooManyOrdinaryEdits |
| `etkile-nir` | Leave | `` | OriginalTokenIsValid |
| `baş-l;mna` | Leave | `` | OriginalTokenIsValid |
| `ı lek` | Leave | `` | OriginalTokenIsValid |
| `sokuluyo-ruz` | Leave | `` | OriginalTokenIsValid |
| `:;iiyledikleri` | Leave | `` | OriginalTokenIsValid |
| `oldukları-ııı` | Leave | `` | OriginalTokenIsValid |
| `oldukla-rırn` | Apply | `olduklarıen` | Accepted |
| `ı ılduğumu` | Leave | `` | OriginalTokenIsValid |
| `i>yle` | Leave | `` | OriginalTokenIsValid |
| `oldu-g` | Leave | `` | OriginalTokenIsValid |
| `ı:ok` | Leave | `` | OriginalTokenIsValid |
| `çoğu-ııu` | Leave | `` | OriginalTokenIsValid |
| `tanıdı-ı^ım` | Leave | `` | OriginalTokenIsValid |
| `ger-1.:ekten` | Leave | `` | OriginalTokenIsValid |
| `esas-lı` | Leave | `` | OriginalTokenIsValid |
| `sömürülüşü-mün` | Leave | `` | TooManyOrdinaryEdits |
| `(daha` | Leave | `` | NoChange |
| `t>anatsal` | Leave | `` | OriginalTokenIsValid |
| `sosyetik-liklerini` | Leave | `` | OriginalTokenIsValid |
| `Auersber-ger` | Leave | `` | OriginalTokenIsValid |
| `ta-ııımlanan` | Leave | `` | OriginalTokenIsValid |
| `çağır-ınışlardı` | Leave | `` | OriginalTokenIsValid |
| `( ;erçekten` | Leave | `` | OriginalTokenIsValid |
| `;ıma` | Leave | `` | OriginalTokenIsValid |
| `;eslendiğini` | Leave | `` | NoChange |
| `Bill-roth` | Leave | `` | OriginalTokenIsValid |
| `yazar-mış` | Leave | `` | OriginalTokenIsValid |
| `Auersber-ger` | Leave | `` | OriginalTokenIsValid |
| `dü-^ünülemeyeceğini` | Leave | `` | OriginalTokenIsValid |
| `Tiyatro-su'ymuş` | Leave | `` | OriginalTokenIsValid |
| `zorlayıcılığın-dan` | Leave | `` | OriginalTokenIsValid |
| `Ek-dal` | Leave | `` | OriginalTokenIsValid |
| `sa-bah` | Leave | `` | OriginalTokenIsValid |
| `ez-lıerlermiş` | Leave | `` | OriginalTokenIsValid |
| `(Strindberg'in)` | Leave | `` | NoChange |
| `(İbsen'in)` | Leave | `` | NoChange |
| `Örde-ği'nin` | Apply | `Ördeği'nin` | Accepted |
| `Soka-gı'na` | Apply | `Sokağı'na` | Accepted |
| `oynanıyor-muş` | Leave | `` | OriginalTokenIsValid |
| `Tiyatro-su'nda` | Leave | `` | OriginalTokenIsValid |
| `donuyor-dum` | Leave | `` | OriginalTokenIsValid |
| `<1rtık` | Leave | `` | OriginalTokenIsValid |
| `(mu` | Leave | `` | OriginalTokenIsValid |
| `oyun-1` | Leave | `` | OriginalTokenIsValid |
| `(bunu` | Leave | `` | OriginalTokenIsValid |
| `dedi, uzun` | Leave | `` | NoChange |
| `değilim,diye` | Leave | `` | NoChange |
| `hakkın-daki` | Leave | `` | OriginalTokenIsValid |
| `Auersber-)',t'r'e` | Leave | `` | OriginalTokenIsValid |
| `za-ıııan` | Apply | `anılan` | Accepted |
| `ı` | Leave | `` | OriginalTokenIsValid |
| `Tiyatro-^aı'nun` | Leave | `` | OriginalTokenIsValid |
| `Tiyatro-^;u'na` | Leave | `` | OriginalTokenIsValid |
| `1` | Apply | `` | Accepted |
| `ola-r<ık` | Leave | `` | OriginalTokenIsValid |
| `Tiyatro-su'ndaki` | Leave | `` | OriginalTokenIsValid |
| `ba-^arısız` | Apply | `başarısız` | Accepted |
| `repertuvar-dan` | Review | `` | MarginTooSmall |
| `İngi-lizler` | Review | `` | MarginTooSmall |
| `oldu-ğunu` | Leave | `` | OriginalTokenIsValid |
| `Tiyatro-su'nda` | Leave | `` | OriginalTokenIsValid |
| `Örde-ği'ni` | Apply | `Ördeği'ni` | Accepted |
| `Strind-berg` | Apply | `Strindberg` | Accepted |
| `Bir-liği'nde` | Leave | `` | OriginalTokenIsValid |
| `zencileri-11in` | Leave | `` | OriginalTokenIsValid |
| `zencileri-11in` | Leave | `` | OriginalTokenIsValid |
| `yal-nız` | Leave | `` | OriginalTokenIsValid |
| `Bill-roth` | Leave | `` | OriginalTokenIsValid |
| `Va-lery` | Leave | `` | OriginalTokenIsValid |
| `Jean-nie` | Leave | `` | OriginalTokenIsValid |
| `Saint-John` | Leave | `` | OriginalTokenIsValid |
| `ancakJean-nie` | Apply | `ancakJeannie` | Accepted |
| `kesiverdi-ğim` | Leave | `` | TooManyOrdinaryEdits |
| `anda,kendimi` | Leave | `` | NoChange |
| `Auersberger-Ier` | Leave | `` | OriginalTokenIsValid |
| `çekilmez-likten` | Apply | `biten` | Accepted |
| `beklerdim,ama` | Leave | `` | OriginalTokenIsValid |
| `Jean-nie` | Leave | `` | OriginalTokenIsValid |
| `sa-lüp` | Leave | `` | OriginalTokenIsValid |
| `yüzü-ne` | Leave | `` | OriginalTokenIsValid |
| `Virgi-nia` | Apply | `Virginia` | Accepted |
| `gösterdi,törende` | Leave | `` | OriginalTokenIsValid |
| `Ala-nı'nın` | Leave | `` | TooManyOrdinaryEdits |
| `Sebasti-an` | Leave | `` | OriginalTokenIsValid |
| `fiyongu-nu` | Leave | `` | OriginalTokenIsValid |
| `Yazm'm-da` | Leave | `` | OriginalTokenIsValid |
| `çe-ken` | Apply | `çeken` | Accepted |
| `sapık-ruh` | Leave | `` | OriginalTokenIsValid |
| `Tiyatro-su'nda` | Leave | `` | OriginalTokenIsValid |
| `sağladığı,mesleği` | Leave | `` | OriginalTokenIsValid |
| `Gert-rude` | Apply | `Gertrude` | Accepted |
| `(ve` | Leave | `` | NoChange |
| `Viya-na'nın` | Leave | `` | OriginalTokenIsValid |
| `çevresindeki-lerle` | Leave | `` | OriginalTokenIsValid |
| `Schre-ker` | Apply | `Schreker` | Accepted |
| `(ve` | Leave | `` | NoChange |
| `ol-sun` | Leave | `` | OriginalTokenIsValid |
| `Schre-ker` | Apply | `Schreker` | Accepted |
| `yazınbi-limciliklerinin` | Review | `` | MarginTooSmall |
| `ciddiyetim-le` | Review | `` | MarginTooSmall |
| `ak-şanı` | Leave | `` | OriginalTokenIsValid |
| `yeme,^ine` | Leave | `` | OriginalTokenIsValid |
| `Za-al'a` | Apply | `Zaal'a` | Accepted |
| `şinzdi-ye` | Apply | `şimdiye` | Accepted |
| `peltek-leşerek` | Leave | `` | OriginalTokenIsValid |
| `peltek-ledi` | Apply | `peltekledi` | Accepted |
| `Metropoli-tan` | Leave | `` | OriginalTokenIsValid |
| `gerçe-ği` | Apply | `gerçeği` | Accepted |
| `Ope-ra'daki` | Apply | `Pera'daki` | Accepted |
| `başladım;birden` | Leave | `` | OriginalTokenIsValid |
| `Jean-nie` | Leave | `` | OriginalTokenIsValid |
| `öfke-lenerek` | Leave | `` | OriginalTokenIsValid |
| `ge-len` | Leave | `` | OriginalTokenIsValid |
| `istiyor-sa` | Leave | `` | OriginalTokenIsValid |
| `halde,gerçek` | Leave | `` | NoChange |
| `Auersberger-ler'den` | Leave | `` | OriginalTokenIsValid |
| `Sokcı-ğı'ndaki` | Apply | `Sokağı'ndaki` | Accepted |
| `ı` | Leave | `` | OriginalTokenIsValid |
| `kop^` | Review | `` | MarginTooSmall |
| `ı` | Leave | `` | OriginalTokenIsValid |
| `^Tj.uk rlc` | Leave | `` | OriginalTokenIsValid |
| `i` | Review | `` | MarginTooSmall |
