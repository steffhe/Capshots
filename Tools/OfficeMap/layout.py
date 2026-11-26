"""The office floor, traced from the 3rd floor plan ("3. ETASJE").

Every coordinate here is in pixels of the floor plan image (x to the right, y down), so the
layout can be checked against the drawing directly. generate.py turns pixels into metres.
Rectangles are (x1, y1, x2, y2).
"""

# Metres per plan pixel. At 0.08 the narrowest corridors are about 1.6 m wide, enough for the
# players (2 m tall, 1 m wide), and the whole floor is about 115 x 83 m.
SCALE = 0.08

# Floor slabs: the building's floor, as rectangles that don't overlap. Together they make the
# outline below; the courtyard (GARDEN) is the hole in the middle.
SLABS = [
    (622, 55, 1347, 342),    # north wing and the corridor north of the courtyard
    (622, 342, 870, 705),    # west wing and the corridor west of the courtyard
    (838, 705, 870, 836),    # narrow corridor from the west wing down to the south wing
    (1098, 342, 1347, 836),  # east wing
    (96, 836, 1347, 1090),   # south wing
]

# The building's outside walls, clockwise (in plan coordinates, i.e. y down)
OUTLINE = [(622, 55), (1347, 55), (1347, 1090), (96, 1090), (96, 836), (838, 836), (838, 705), (622, 705)]

# The courtyard garden (on the same level as the floor, open to the sky)
GARDEN = (870, 342, 1098, 836)

# Rooms: (kind, label, rect, name). Rooms with the same label are one room (no wall between them).
# kinds: office (carpet, desks), meeting (table and chairs), quiet (armchair), print, gym
OFFICES = [
    ("32", (622, 55, 730, 162)),
    ("31", (730, 55, 840, 125)),
    ("30", (840, 55, 877, 125)),
    ("29", (877, 55, 932, 125)),
    ("28", (932, 55, 1063, 125)),
    ("27b", (1063, 55, 1100, 125)),
    ("27a", (1100, 55, 1133, 125)),
    ("26b", (1133, 55, 1168, 125)),
    ("26a", (1168, 55, 1205, 125)),
    ("25", (1205, 55, 1347, 200)),
    ("24", (1205, 200, 1347, 312)),
    ("Drop-in", (1205, 312, 1347, 452)),
    ("33", (622, 162, 770, 237)),
    ("34", (622, 237, 770, 312)),
    ("35", (622, 312, 770, 387)),
    ("36", (622, 387, 770, 462)),
    ("37", (622, 462, 770, 570)),
    ("38", (622, 570, 768, 705)),
    ("39", (768, 645, 838, 705)),
    ("40", (1293, 645, 1347, 683)),
    ("41", (1293, 683, 1347, 722)),
    ("42", (1293, 722, 1347, 762)),
    ("43", (1251, 762, 1347, 911)),
    ("44b", (1168, 911, 1347, 985)),
    ("44a", (1050, 985, 1347, 1090)),
    ("58", (243, 836, 315, 932)),
    ("59", (315, 836, 392, 932)),
    ("60", (392, 836, 538, 932)),
    ("61", (538, 836, 740, 932)),
    ("57", (170, 1014, 206, 1090)),
    ("56", (206, 1014, 243, 1090)),
    ("55", (243, 1014, 280, 1090)),
    ("54", (280, 1014, 317, 1090)),
    ("53", (390, 1014, 538, 1090)),
    ("52", (538, 1014, 573, 1090)),
    ("51", (646, 1014, 684, 1090)),
    ("50", (684, 1014, 720, 1090)),
    ("49", (720, 1014, 757, 1090)),
    ("48", (757, 1014, 794, 1090)),
    ("47b", (794, 1014, 829, 1090)),
    ("47a", (829, 1014, 941, 1090)),
    ("46", (941, 1014, 976, 1090)),
    ("45b", (976, 1014, 1013, 1090)),
    ("45a", (1013, 1014, 1050, 1090)),
]

MEETING = [
    ("A", "Hulrommet", (1122, 408, 1175, 452)),
    ("B", "Luftrommet", (1122, 315, 1175, 408)),
    ("C", "Tilfluktsrommet", (1097, 153, 1170, 218)),
    ("D", "Tidsrommet", (1052, 153, 1097, 218)),
    ("E", "Verdensrommet", (942, 153, 1052, 218)),
    ("F", "Albuerommet", (905, 153, 942, 218)),
    ("G", "Tomrommet", (868, 153, 905, 218)),
    ("H", "Bakrommet", (796, 153, 868, 218)),
    ("I", "Motorrommet", (796, 382, 848, 435)),
    ("J", "Prøverommet", (796, 435, 848, 487)),
    ("K", "Gårdsrommet", (796, 487, 848, 548)),
    ("L", "Mellomrommet", (1123, 799, 1176, 838)),
    ("M", "Hanskerommet", (1078, 932, 1133, 982)),
    ("N", "Hvilerommet", (573, 1014, 646, 1090)),
    ("O", "Kjølerommet", (317, 1014, 390, 1090)),
    ("P", "Akterrommet", (96, 1014, 170, 1090)),
    ("Q", "Samhandlingsrommet", (270, 932, 315, 989)),
    ("R", "Tegnerommet", (315, 932, 366, 989)),
    ("S", "Hjerterommet", (610, 932, 686, 989)),
]

QUIET = [
    ("SR2", "Egorommet", (1130, 270, 1175, 315)),
    ("SR3", "Muterommet", (796, 308, 850, 345)),
    ("SR4", "Roligrommet", (1123, 762, 1176, 799)),
    ("SR5", "Enerommet", (1133, 932, 1168, 982)),
]

PRINT = [
    (796, 345, 850, 382),
    (406, 932, 445, 989),
]

# The gym is L-shaped: the main room and the entrance strip west of it
GYM = [
    (1176, 762, 1251, 909),
    (1123, 838, 1176, 909),
]

# Closed blocks you can't go into: stairwells, lifts, toilets, shafts and the cloakroom.
# (kind, sign, rect). They get a door (just for show) and a sign on the corridor side.
CORES = [
    ("stair", "Nødutgang", (965, 268, 1053, 312)),
    ("wc", "WC", (1053, 268, 1100, 312)),
    ("store", "Garderobe", (812, 220, 868, 285)),
    ("shaft", "", (720, 572, 780, 612)),
    ("stair", "Utgang 28", (1122, 478, 1175, 610)),
    ("wc", "WC", (1175, 478, 1262, 640)),
    ("lift", "Heis", (1122, 650, 1155, 705)),
    ("wc", "WC", (1155, 650, 1197, 705)),
    ("stair", "Trapp", (1200, 650, 1262, 705)),
    ("stair", "Nødutgang", (152, 863, 240, 914)),
    ("shaft", "", (125, 863, 152, 914)),
    ("stair", "Utgang 26", (740, 836, 838, 970)),
    ("wc", "WC", (710, 935, 740, 970)),
    ("lift", "Heis", (877, 860, 931, 970)),
]

# Free-standing furniture in the corridors and lounges: (kind, rect or point, options)
# sofa/armchair "back" is the side the backrest is on: n, s, e or w (plan directions, n = up).
HALL_FURNITURE = [
    # Lounge by the cloakroom, north of the courtyard
    ("sofa", (872, 236, 882, 272), {"back": "w"}),
    ("sofa", (920, 236, 930, 272), {"back": "e"}),
    ("sofa", (884, 226, 918, 234), {"back": "n"}),
    ("table", (888, 242, 914, 266), {}),
    ("rings", (901, 254), {}),
    ("counter", (1003, 256, 1050, 266), {"coffee": True}),
    ("bench", (1125, 234, 1160, 250), {}),
    ("plant", (950, 230), {}),
    ("plant", (1085, 325), {}),
    # By K, at the south end of the west corridor
    ("counter", (800, 550, 846, 560), {"coffee": True}),
    ("wingbacks", (822, 594), {}),
    ("plant", (780, 625), {}),
    # East lounge, by Utgang 28
    ("counter", (1263, 520, 1272, 568), {"coffee": True}),
    ("hightable", (1276, 482, 1290, 512), {}),
    ("hightable", (1276, 588, 1290, 618), {}),
    ("sofa", (1336, 484, 1346, 520), {"back": "e"}),
    ("sofa", (1336, 548, 1346, 590), {"back": "e"}),
    ("armchair", (1300, 545, 1312, 557), {"back": "w"}),
    ("armchair", (1300, 580, 1312, 592), {"back": "w"}),
    ("table", (1316, 556, 1330, 582), {}),
    ("rings", (1323, 569), {}),
    ("slats", (1295, 474, 1297, 530), {}),
    ("plinth", (1300, 625), {}),
    ("plant", (1340, 625), {}),
    ("plant", (1340, 465), {}),
    # Between the east lifts and the gym
    ("counter", (1124, 708, 1152, 716), {"coffee": True}),
    ("bench", (1196, 728, 1232, 744), {}),
    ("plant", (1280, 750), {}),
    # South-west lounge
    ("sofa", (118, 936, 128, 976), {"back": "w"}),
    ("table", (138, 944, 172, 972), {}),
    ("rings", (155, 958), {}),
    ("armchair", (188, 940, 202, 954), {"back": "e"}),
    ("armchair", (188, 962, 202, 976), {"back": "e"}),
    ("plant", (110, 1000), {}),
    # By the south print room
    ("counter", (446, 936, 456, 976), {"coffee": True}),
    ("roundtable", (486, 962), {}),
    ("plant", (560, 1000), {}),
    # South lounge between the lift and the east wing
    ("sofa", (1026, 866, 1036, 912), {"back": "e"}),
    ("armchair", (976, 870, 990, 884), {"back": "w"}),
    ("armchair", (976, 894, 990, 908), {"back": "w"}),
    ("table", (996, 874, 1018, 904), {}),
    ("hightable", (1064, 880, 1100, 896), {}),
    ("rings", (1004, 889), {}),
    ("counter", (932, 890, 940, 924), {"coffee": True}),
    ("booth", (1043, 932, 1061, 982), {}),
    ("booth", (1061, 932, 1078, 982), {}),
    ("plant", (1090, 850), {}),
    ("plant", (860, 850), {}),
]

# The courtyard garden, at the same level as the floor and walled in glass that breaks. A gravel
# garden in the style of the real one: planted beds with corten edges, dark paving paths, a wooden
# garden house with a glass roof in the middle (open at both ends: a way through), a vent stack and
# benches. Rects are plan rects as above.
GARDEN_DOORS = [("n", 984), ("s", 984), ("w", 450), ("w", 730), ("e", 450), ("e", 730)]  # (side, position along it)
GARDEN_PATHS = [
    (972, 342, 996, 836),   # north-south, through the garden house
    (870, 438, 1098, 462),  # east-west, between the north doors
    (870, 718, 1098, 742),  # east-west, between the south doors
]
# (rect, what's planted): conifer, shrubs, tree (a bare young tree), grass
GARDEN_BEDS = [
    ((882, 356, 958, 430), "conifer tree shrubs"),
    ((1010, 356, 1086, 430), "shrubs grass"),
    ((882, 470, 944, 530), "shrubs"),
    ((1024, 470, 1086, 530), "conifer shrubs"),
    ((882, 648, 944, 708), "conifer shrubs"),
    ((1024, 648, 1086, 708), "shrubs"),
    ((882, 750, 958, 824), "shrubs grass"),
    ((1010, 750, 1086, 824), "conifer tree shrubs"),
]
GARDEN_HOUSE = (959, 539, 1009, 639)  # open at the north and south ends
GARDEN_BENCHES = [(905, 580, 935, 586), (1033, 592, 1063, 598)]
GARDEN_VENT = (1040, 560, 1062, 574)
GARDEN_LIGHTS = [(876, 445), (1092, 735), (978, 352), (990, 826), (950, 600), (1018, 580)]

# Where players appear: (x, y, facing, team) with facing in degrees on the plan, 0 = up (north),
# 90 = right (east). Team 1 starts in the north wing, team 0 in the south wing; in free for all
# everyone uses them all. Checked by generate.py: no spawn point can see one of the other team's.
SPAWNS = [
    # North wing
    (760, 139, 90, 1),
    (880, 139, 90, 1),
    (1010, 140, 270, 1),
    (1120, 139, 270, 1),
    (783, 260, 180, 1),
    (1190, 230, 180, 1),
    (1115, 245, 180, 1),
    (1190, 345, 180, 1),
    # South wing
    (300, 1001, 90, 0),
    (500, 1001, 90, 0),
    (700, 1001, 270, 0),
    (860, 1001, 270, 0),
    (1020, 958, 270, 0),
    (1140, 920, 0, 0),
    (856, 905, 0, 0),
    (1262, 728, 270, 0),
]

# The camera shown while you're dead: in the courtyard's south-west corner, looking north over
# the garden. (x, y, height in metres above the floor, pitch, facing)
MAP_CAMERA = (882, 826, 6.0, 18, 15)
