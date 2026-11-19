# Plant and root measurements: what the sources say

Collected 19 Oct 2026 for the "deeper, not bigger" work. Only numbers read from the sources are listed. Where a source gave nothing usable, that is said too. Nothing here has been checked against a real plant by us.

## Above ground

| Crop | Measured value | Source |
|---|---|---|
| Tomato | leaves up to 30 cm long and 10 cm wide; plant erect or sprawling, about 1.5 m | [VicFlora, Solanum lycopersicum](https://vicflora.rbg.vic.gov.au/flora/taxon/bede19ce-e3ad-48cd-a1bb-69d1348bfdb8); [NZ Flora](https://nzflora.info/factsheet/Taxon/Solanum-lycopersicum.html) (leaves to 30 cm, often much less) |
| Pepper | 20-80 cm tall; petiole 4-7 cm; leaf blade 4-13 x 1.5-4 cm | [Flora of China, Capsicum annuum](http://www.efloras.org/florataxon.aspx?flora_id=2&taxon_id=200020513) |
| Potato | 30-80 cm tall; petiole 2.5-5 cm; leaves interrupted-odd-pinnate with 6-8 pairs of leaflets plus small interstitial leaflets (Flora of Iraq says 2-4 large pairs, so the floras disagree) | [Flora of China, Solanum tuberosum](http://www.efloras.org/florataxon.aspx?flora_id=2&taxon_id=200020608) |
| Apple | leaves about 7-10.5 cm long and 3-7.5 cm wide; trees 2.1-4.3 m in the measured cultivars | [US plant patents for single cultivars, e.g. Wuranda](https://image-ppubs.uspto.gov/dirsearch-public/print/downloadPdf/PP35577) (single cultivars, not the species) |

## Roots

| Crop | What is known | Source |
|---|---|---|
| Tomato | Seed-grown plants have a taproot with laterals spreading sideways; about 60% of roots are in the top ~28 cm. Seedling numbers (in vitro, not soil): lateral root growth angle 123.5 +- 20.5 degrees (Moneymaker), 7.0 +- 1.6 adventitious roots 10 days after induction. Lateral root density is only given as a figure, not a number. | [Virginia Cooperative Extension](https://pubs.ext.vt.edu/SPES/spes-508/spes-508.html); [Alaguero-Cordovilla et al. 2018](https://pmc.ncbi.nlm.nih.gov/articles/PMC6321557/) |
| Potato | Shallow fibrous system on adventitious roots; usually 40-50 cm deep, up to about 1 m without obstructions. No lateral spread value found. | [Potato root system development and architecture](https://dspace.bits-pilani.ac.in/items/7b122213-c4a9-455b-be70-6982cb96e322) (abstract only) |
| Pepper | Taproot at first but fairly fibrous in the top 0-30 cm; primary root goes down, secondary and tertiary roots spread sideways; in greenhouse transplants most laterals lay in one plane. No spread value found. | [Romanian sweet pepper study](https://horticulturejournal.usamv.ro/pdf/2025/issue_2/Art45.pdf); [HortScience 1987](https://journals.ashs.org/downloadpdf/view/journals/hortsci/22/3/article-p418.pdf) |
| Apple | Roots reach well beyond the branch spread; fine roots are densest near the trunk and thin out with distance; fine-root density peaks at 20-30 cm depth in older trees. No clean spread-to-crown ratio found. | [Wisconsin digital library](https://search.library.wisc.edu/digital/A62TBSCSQ3TYCR9D/text/A35VCVWZNTCIDF8Q); [Loess Plateau study](https://ideas.repec.org/a/mth/jas888/v7y2019i2p160-175.html) |

## What the search could not find

* No open dataset of real root scans (RSML files) for tomato, potato, pepper or apple. The only open RSML data found is simulated ([Zenodo 208214](https://zenodo.org/records/208214)).
* No tomato or potato parameter file for the CRootBox/CPlantBox root simulator turned up (only maize, barley and wheat examples); the CPlantBox repository itself was not searched.
* No per-cm lateral root density or adult-plant root spread for tomato, pepper or potato.

So the root *shape* in the app (a spreading, branching, fibrous system with hairs) is supported only qualitatively. Measured sizes and angles exist only for tomato seedlings.

## How the current plants compare (twin sizes are in metres)

| Crop | Twin height | Largest leaf now | Leaf / height now | Leaf / height in the sources |
|---|---|---|---|---|
| Tomato | 1.0 | 0.52 | 0.52 | about 0.2 (30 cm on a 1.5 m plant) |
| Pepper | 0.75 | 0.24 | 0.32 | 0.05-0.65 (4-13 cm on 20-80 cm) |
| Potato | 0.62 | 0.30 | 0.48 | not found |
| Apple | 1.2 | 0.15 | 0.13 | about 0.03 (7-10 cm on 2-4 m) |

The twin's leaves are larger than real proportions, mostly to keep the plants leafy and readable on a phone.


---

# The ten crops added with the full PlantVillage set (1 Nov 2026)

Same rule as above: only numbers read from the sources; anything the sources did not give is marked **assumed**.

## Above ground

| Crop | Measured value | Source |
|---|---|---|
| Blueberry (highbush) | 1-5 m tall; blades 15-70 x 10-25 mm | [Flora of North America, Vaccinium corymbosum](http://www.efloras.org/florataxon.aspx?flora_id=1&taxon_id=242417401) |
| Sweet cherry | tree to ~25 m; petiole 2-7 cm; blade 3-13 x 2-6 cm | [Flora of China, Cerasus avium](http://www.efloras.org/florataxon.aspx?flora_id=2&taxon_id=200010640) |
| Corn (maize) | culms 1-4 m; blades 50-90 x 3-12 cm | [Flora of China, Zea mays](http://www.efloras.org/florataxon.aspx?flora_id=2&taxon_id=200026507) |
| Grape | blade 7-18 x 6-16 cm, 3-5-lobed; petiole 4-9 cm; tendrils forked, opposite the leaves | [Flora of China, Vitis vinifera](http://www.efloras.org/florataxon.aspx?flora_id=2&taxon_id=200013507) (FNA gives blades 12-20 cm) |
| Sweet orange | leaves 7.5-11 cm; fruit 6-9 cm across | [Flora of Pakistan, Citrus sinensis](http://www.efloras.org/florataxon.aspx?flora_id=5&taxon_id=200012435); a cultivar patent gives a 2.74 m tree at nine years |
| Peach | trees 3-8 m; petiole 1-2 cm; blade 7-15 x 2-3.5 cm | [Flora of China, Amygdalus persica](http://www.efloras.org/florataxon.aspx?flora_id=2&taxon_id=200010618) |
| Red raspberry | shrub 1-2 m; 5-7 leaflets (FNA: 3-5, rarely 7); terminal leaflet 3-8 cm (FOC) or 7-15 cm (FNA) | [Flora of China / FNA, Rubus idaeus](http://www.efloras.org/florataxon.aspx?flora_id=1&taxon_id=200011446) |
| Soybean | erect annual 0.3-2 m (Flora of Pakistan); pinnately trifoliolate; petiole 2-20 cm; terminal leaflet 5-12 x 2.5-8 cm | [Flora of China, Glycine max](http://www.efloras.org/florataxon.aspx?flora_id=2&taxon_id=242323602) |
| Squash | C. pepo petiole 6-9 cm, blade triangular-ovate, cordate, toothed (no size given); C. maxima blade 15-25 cm, petiole 15-20 cm; C. moschata blade 12-25 x 20-30 cm | [Flora of China, Cucurbita pepo](http://www.efloras.org/florataxon.aspx?flora_id=2&taxon_id=200022621) |
| Strawberry | herb 10-40 cm; petiole 2-10 cm; three leaflets 3-7 x 2-6 cm | [Flora of China, Fragaria x ananassa](http://www.efloras.org/florataxon.aspx?flora_id=2&taxon_id=242322784) |

## Roots

| Crop | What is known | Source |
|---|---|---|
| Blueberry | Fine, fibrous, shallow (most in the top 23-30 cm), **no root hairs**; mycorrhizal fungi take their place | [Extension: Blueberry botany](https://blueberries.extension.org/blueberry-botany), [Tennessee Extension](https://vegetables.tennessee.edu/wp-content/uploads/sites/167/2022/03/Blueberry-Prod.-Pick-TN-22-1.pdf) |
| Corn | Nodal roots in whorls from the stem: crown roots below ground and **brace roots** above it; the lowest whorls enter the soil | [Maize brace roots provide stalk anchorage (PMC7649601)](https://www.ncbi.nlm.nih.gov/pmc/articles/PMC7649601/) |
| Soybean | **Nodules** on the taproot and laterals (a few to several hundred per plant); nodule size not found | [SNAP nodule pipeline (PMC8343430)](https://pmc.ncbi.nlm.nih.gov/articles/PMC8343430) |
| Strawberry | Shallow: 80-90% of roots in the top 15 cm of clay, 50% in sandy loam | [Manitoba Agriculture](https://gov.mb.ca/agriculture/crops/crop-management/fruit-crops/strawberry-the-plant.html) |
| Squash | Taproot to about 1 m; 10-12 horizontal laterals at 20-40 cm depth reaching 4-5 m (C. maxima) | [AgroAtlas, Cucurbita maxima](https://agroatlas.ru/en/content/cultural/Cucurbita_maxima_K/index.html) |
| Orange | Shallow feeder roots, most in the top 30 cm, spreading past the canopy | [UC Extension slides](https://class.ucanr.edu/sites/default/files/2023-06/385572.pdf) |
| Cherry, peach, grape, raspberry | Not searched beyond general tree/vine root habit; drawn like the apple's woody roots | - |

## How the twins use them

| Crop | Twin height | Scale (assumed real height) | Leaf in the twin | Ratio tested | Range from the sources |
|---|---|---|---|---|---|
| Blueberry | 1.1 m | 0.61 (1.8 m bush, assumed) | blade 3.4 cm | 0.031 | 0.003-0.07 |
| Cherry | 1.3 m | 0.33 (4 m orchard tree, assumed) | blade 3.0 cm | 0.023 | 0.005-0.045 (tree height assumed 3-6 m) |
| Corn | 1.6 m | 0.64 (2.5 m) | blade 44 cm | 0.275 | 0.125-0.9 |
| Grape | 1.3 m | 0.68 (1.9 m trained vine, assumed) | blade 8.6 cm, petiole 0.52 of blade | 0.066 | 0.028-0.12 (vine height assumed) |
| Orange | 1.2 m | 0.34 (3.5 m, assumed) | blade 3.1 cm; fruit 2.6 cm across | 0.026 | 0.009-0.045; fruit/leaf 0.55-1.2 |
| Peach | 1.25 m | 0.31 (4 m) | blade 3.4 cm | 0.027 | 0.009-0.05 |
| Raspberry | 1.3 m | 0.87 (1.5 m) | whole leaf 20 cm | 0.154 | 0.03-0.3 (whole leaf assumed 2x the terminal leaflet) |
| Soybean | 0.8 m | ~0.9 | terminal leaflet 8.6 cm, petiole 1.1 of leaflet | 0.107 | 0.025-0.4 |
| Squash | 0.35 m | 1 | blade 21 cm, petiole 0.4 of blade | 0.61 | 0.15-0.83 (plant height assumed 0.3-0.8 m) |
| Strawberry | 0.15 m (the leafy clump; the flora's 10-40 cm includes flower stalks) | 1 | leaflet 7 cm, petiole 1.43 of leaflet (10 cm), the top of both ranges | 0.47 | 0.075-0.7 |

Fruit sizes that the sources did not give (blueberry, cherry, peach, raspberry, strawberry, squash, corn ear, soybean pod) are **assumed** from common knowledge and scaled with the leaves.


## Squash, redone (1 Nov 2026)

The first squash took its petiole length (6-9 cm) from the Flora of China, which gives no blade size for *C. pepo*; it produced a flat, unconvincing plant. Better sources:

| Measured value | Source |
|---|---|
| Leaves large, ascending to erect, dark green; petioles hollow, 200-370 mm long, to 19 mm across; blade roundish, 110-340 x 120-390 mm, palmately (3-)5-7-lobed, broad lobes, cordate, toothed; stems ridged, to 25 mm across; flowers yellow-orange, 120-180 mm across, staminate flowers on 150-330 mm upright stalks | [Prigge & Gibson, *Cucurbita pepo* var. *pepo*, UCLA Santa Monica Mountains flora](https://www.smmflowers.org/bloom/ANF-descriptions/Cucurbita_pepo_pepo_UCLA_SantaMonicas.pdf) |
| Plants 0.7-1.3 m tall; blades 20-30 x 15-25 cm; petioles 15-25 cm; flowers 9-15 cm wide | [KPU plant database, Cucurbita pepo](https://plantdatabase.kpu.ca/Plant/cupe) |
| Bush zucchini about 75 cm tall, 1.2 m across in a spacing/fertiliser trial | [FAO AGRIS record](https://agris.fao.org/search/fr/records/64e8bd6f6a37d977ef6cd800) |

The twin: 0.7 m tall; blade 27 cm, petiole 1.25x the blade (tested against 0.5-3.4 from the two sources); blade/height 0.38 (tested against 0.085-0.49); flowers 15 cm across.

## Exception: lusher fruit-tree crowns

At the user's request, apple, cherry, orange and peach draw their canopy leaves 1.7-1.8 times their real size, with more leaves per twig, so the crowns read as full. This is a deliberate departure from the proportions above, for appearance only; the profiles' `leafSize` keeps the measured value.

## Habit check of all 14 plants (2 Nov 2026)

Each twin was rendered from the side and from above and measured (`docs/screenshots/plants/audit.txt`, from `PlantGallery`).
Corrected: tomato fruit in trusses of three (tomatoes fruit in clusters on a stalk between the leaves); blueberry stems lengthened
so the bush reaches its 1.1 m; corn's tassel now inside the plant's height; raspberry canes erect, arching only at the tips; squash
flowers among the leaf stalks rather than above the plant, and young leaves standing up (ascending to erect, Prigge & Gibson), which
makes it 0.58 m; strawberry leaves at the top of the flora's petiole and leaflet ranges, standing up from the crown; potato a clump
about as wide as tall. Pepper, apple, cherry, orange, peach, grape and soybean were checked and left as they were (see
`docs/DECISIONS.md` 59-67).

## Potato leaf, sourced (10 Nov 2026)

Solanaceae Source (Spooner & Knapp), *Solanum tuberosum*: leaves odd-pinnate, blade 8-22 x 5-13 cm, petiole 2-6 cm, 3-8 pairs of
lateral leaflets, 0-45 small interjected leaflets (https://solanaceaesource.myspecies.info/node/316552). The potato twin's leaf is now
22 cm on a 0.62 m plant with two small interjected leaflets in each gap; the ratio test is 0.08-0.47 (8-28 cm on 0.6-1 m plants)
instead of the tomato bound it borrowed before.

## The PlantSeg plants (10 Nov 2026)

Measurements used for the 18 new twins. Twins are drawn smaller than the real plant where it is a tree or a tall crop (scale given
with each profile in `Assets/Editor/PlantSegSpecies.cs`); leaf-to-plant ratios are kept.

| Plant | Measured values | Source |
|---|---|---|
| Banana (*Musa acuminata*, AAA) | pseudostem to ~5 m, under 25 cm across; petiole ~80 cm; blade oblong 1.9-2.3 m x 50-70 cm (genus: 1.5-4 m x 0.7-1 m) | [World Flora DB, M. acuminata](https://worldfloradb.net/plants/musa-acuminata-colla); [PROSEA Musa](https://uses.plantnet-project.org/en/Musa_(PROSEA_Fruits)) |
| Basil (*Ocimum basilicum*) | herb 20-80 cm (to 1 m); leaves opposite, blade 2.5-5 x 1-2.5 cm (to 8 cm), petiole 1-2 cm | [World Flora DB](https://worldfloradb.net/plants/ocimum-basilicum-l); [PROSEA](https://uses.plantnet-project.org/en/Ocimum_basilicum_(PROSEA)); [Flora Malesiana](https://portal.cybertaxonomy.org/flora-malesiana/cdm_dataportal/taxon/b99dc84c-2400-4110-9ad4-2d30545575c4) |
| Common bean (*Phaseolus vulgaris*) | trifoliate, leaflets 6-15 x 3-11 cm, terminal leaflet on a longer stalk; petiole to 15 cm; pod linear, to 20 cm | [PROTA](https://uses.plantnet-project.org/en/Phaseolus_vulgaris_-_French_bean_(PROTA)); [Michigan climbers](https://climbers.lsa.umich.edu/phaseolus-vulgaris) |
| Broccoli, cabbage, cauliflower (*Brassica oleracea*) | basal leaves with petioles to 30 cm, blades to 40-45 x 15 cm; plants 0.6-1.5 m in flower; broccoli upright to 1 m, head 7-20 cm across | [iNaturalist flora text](https://www.inaturalist.org/guide_taxa/338660); [ASU](https://biokic4.rc.asu.edu/sandbox/dev/taxa/index.php?tid=17053); [Yara](https://english-master.yara.co.uk/crop-nutrition-master/vegetable-brassicas/vegetable-brassica-types) |
| Coffee (*Coffea arabica*) | shrub to 5 m unpruned, usually kept at 1.2-1.8 m; leaves opposite on lateral branches, elliptic, to 10-12 cm, short petioles; drupe red, 1-2 cm | [World Agroforestry](https://apps.worldagroforestry.org/treedb2/speciesprofile.php?Spid=547); [UF/IFAS](https://ask.ifas.ufl.edu/hs306); [Kew POWO](https://powo.science.kew.org/taxon/urn:lsid:ipni.org:names:747038-1/general-information); [Brisbane botany](https://www.botanybrisbane.com/plants/rubiaceae/coffea/coffea-arabica/) |
| Cucumber (*Cucumis sativus*) | leaves 7-20 x 7-20 cm, 3-7-lobed, cordate; petiole 10-16(-20) cm; tendrils simple, to 30 cm; fruit 5-50 cm, tuberculate | [World Flora DB](https://worldfloradb.net/plants/cucumis-sativus-l); [PROSEA](https://uses.plantnet-project.org/en/Cucumis_sativus_(PROSEA)); [Go Botany](https://gobotany.nativeplanttrust.org/species/cucumis/sativus) |
| Eggplant (*Solanum melongena*) | 0.5-1.5 m (to 2); blade ovate 3-25 x 2-15 cm, petiole 6-10 cm (PROTA says 1-10); flowers purple, 3-5 cm across; fruit 5-30 cm | [PROTA](https://uses.plantnet-project.org/en/Solanum_melongena_(PROTA)); [Kew POWO](https://powo.science.kew.org/taxon/urn:lsid:ipni.org:names:820053-1/general-information); [Go Botany](https://gobotany.nativeplanttrust.org/species/solanum/melongena/) |
| Garlic (*Allium sativum*) | 4-10 distichous leaves, blade linear, to 50 x 2.5 cm, flat or V-shaped; bulb to 7 cm across | [PROSEA](https://uses.plantnet-project.org/en/Allium_sativum_(PROSEA)) |
| Ginger (*Zingiber officinale*) | leafy pseudostems 30-100 cm from the rhizome; 8-12 distichous leaves, linear-lanceolate, 5-30 cm x 8-20 mm | [Oxford Plants 400](https://herbaria.plants.ox.ac.uk/bol/plants400/Profiles/YZ/Zingiberofficinale); [NC State](https://plants.ces.ncsu.edu/plants/zingiber-officinale/common-name/ginger) |
| Lettuce (*Lactuca sativa*) | basal rosette of leaves to about 15 cm (looseleaf); head lettuce: outer leaves overlap while inner ones grow into the head | [KPU](https://plantdatabase.kpu.ca/PlantPrint/lasa); [UF/IFAS](https://gardeningsolutions.ifas.ufl.edu/mastergardener/resources/plantid/vegetables/lettuce) |
| Celery (*Apium graveolens*) | to 1 m in flower; leaves pinnate to bipinnate, leaflets 3-6 x 2-4 cm, on long fleshy ribbed petioles | [vectree / flora text](https://vectree.io/c/apium-graveolens) |
| Sycamore maple (*Acer pseudoplatanus*) | tree to 25-30 m; leaf 5-lobed, 10-25 cm long and broad; petiole 5-15 (5-20) cm | [VicFlora](https://vicflora.rbg.vic.gov.au/flora/taxon/f9f0b3aa-8980-44fb-b0b5-ce9af79b4e58); [Virginia Tech dendrology](https://dendro.cnre.vt.edu/DENDROLOGY/syllabus/factsheet.cfm?id=162) |
| Plum (*Prunus domestica*) | tree to 6-10 m; leaves elliptic to obovate, 4-10 x 1.5-5 cm, petiole 1-2 cm; drupe 1-3.5 cm (to 6 cm cultivated) | [VicFlora](https://vicflora.rbg.vic.gov.au/flora/taxon/bbbc69c7-f079-4f33-8ae3-25ec7408e943); [NZ Flora](https://www.nzflora.info/factsheet/taxon/Prunus-xdomestica.html) |
| Rice (*Oryza sativa*) | culms 0.5-1.5 m; blades 25-60 x 0.5-2 cm; panicle to 30 cm | [naturelib, Flora of China text](https://naturelib.net/plantae/oryza-sativa/) |
| Tobacco (*Nicotiana tabacum*) | 0.5-2 m (to 3); leaves 20-50 x 3-15 cm (lower to ~30 x 12); corolla funnelform 3.5-5 cm, pink lobes, limb 1-1.5 cm | [Flora of China via eFloras excerpt]; [NZ Flora](https://nzflora.info/factsheet/Taxon/Nicotiana-tabacum.html); [VicFlora](https://vicflora.rbg.vic.gov.au/flora/taxon/6c642775-b214-4f3f-ac24-695168fd8664) |
| Wheat (*Triticum aestivum*) | culms 40-170 cm; blade linear 15-40 x 1-2 cm; spike 4-18 cm | [PROTA](https://uses.plantnet-project.org/en/Triticum_aestivum_(PROTA)); [Grass Genera of the World](https://www.delta-intkey.com/grass/www/triticum.htm) |

Not found as a measured flora value, so taken from common horticultural figures (marked "assumed" in the profiles): carrot foliage
30-60 cm with tripinnate leaves and a 10-25 cm root; cabbage head 10-25 cm on a plant about 40 cm tall and 60 cm across; cauliflower
curd 12-20 cm; lettuce head 15-25 cm across.

## Mango, sugarcane, chilli (11 Nov 2026)

| Plant | Measured values | Source |
|---|---|---|
| Mango (*Mangifera indica*) | tree 5-40 m; leaves oblong-lanceolate 10-30 x 3.5-7 cm, petiole 0.8-6 cm; drupe 5-20 cm | [World Flora DB](https://worldfloradb.net/plants/mangifera-indica-l); [NParks](https://www.nparks.gov.sg/florafaunaweb/flora/3/0/3013) |
| Sugarcane (*Saccharum officinarum*) | culms 3-6 m, 2-5 cm thick, clumped; blades 70-150 x 2-6 cm | [Flora of North America](https://floranorthamerica.org/Saccharum_officinarum); [AusGrass](https://ausgrass2.myspecies.info/content/saccharum-officinarum) |
| Chilli (*Capsicum annuum*, hot types) | 0.3-1.2 m; leaves to 7.5-12 cm; fruit 7-12 cm (cayenne ~12 cm) | [Britannica](https://www.britannica.com/plant/Capsicum-annuum); [RHS cayenne](https://rhs.org.uk/plants/325164/capsicum-annuum-cayenne-red/details); [VicFlora](https://hortflora.rbg.vic.gov.au/taxon/ad9df8f8-5340-11e7-b82b-005056b0018f) |

Twins: mango a young 4 m tree at 0.33 scale; sugarcane a 3.5 m clump at 0.5 scale; chilli at full size (0.7 m).
