"""Strata F4 authored shotgun shell over the existing grip and muzzle contract.

All dimensions are offline mesh content in metres. The negative Y direction is
forward in the Blender source; exported glTF is +Z forward.
"""


def build(api):
    box = api['box']
    ring = api['ring']
    panel = api['panel']
    tube = api['tube']
    axial_shell = api['axial_shell']
    sculpt = api['sculpt']
    screw = api['screw']
    marking = api['marking']
    join = api['join']
    parts = api['parts']
    white = api['white']
    navy = api['navy']
    metal = api['metal']
    rubber = api['rubber']
    dark = api['dark']
    oxblood = api['oxblood']
    light = api['light']
    thread = api['thread']
    parts.clear()

    # One long, closed chassis joins the aft receiver, grip and forward support.
    box('Strata continuous chassis', (0, -.47, .115), (.194, 1.08, .23), navy, .022)
    box('Strata raised receiver', (0, -.35, .275), (.19, .86, .16), navy, .017)
    box('Strata grip bridge', (0, -.025, -.030), (.148, .29, .20), navy, .020)
    # The preserved right palm is offset to the receiver's left side. This
    # continuous rubber swell fills the palm, while fingers close below it.
    box('Strata primary palm grip', (-.086, -.07, -.185), (.153, .28, .22), rubber, .026)
    box('Strata foregrip bridge', (0, -.60, -.023), (.165, .58, .145), navy, .017)
    axial_shell('Strata grip', [(.045,-.26,.050,.045),(.017,-.22,.063,.065),(-.023,-.14,.065,.10),(-.058,-.07,.052,.07)], rubber)
    box('Strata trigger well', (0, -.19, -.125), (.115, .15, .095), dark, .012)
    tube('Strata enclosed trigger guard', [(0,.025,-.045),(0,-.15,-.048),(0,-.29,-.09),(0,-.26,-.19),(0,-.105,-.19),(0,.025,-.12)], .009, metal, True)
    box('Strata foregrip saddle', (0,-.69,-.108),(.18,.42,.060),rubber,.013)
    tube('Strata foregrip stitched seam',[(.088,y,-.105) for y in (-.86,-.77,-.68,-.59,-.50)],.001,thread)

    # Twin vertically stacked square shrouds surround real inset cylindrical bores.
    for index, z in enumerate((.140,.350)):
        box('Strata barrel dark spine '+str(index),(0,-.655,z),(.165,1.01,.145),navy,.011)
        box('Strata barrel upper ceramic '+str(index),(0,-.67,z+.073),(.195,.92,.035),white,.008)
        box('Strata barrel lower ceramic '+str(index),(0,-.67,z-.072),(.195,.92,.028),white,.007)
        for side in (-1,1):
            box('Strata barrel side ceramic '+str(index),(side*.089,-.655,z),(.026,.94,.153),white,.008)
            box('Strata inset side rail '+str(index),(side*.106,-.61,z-.012),(.006,.64,.023),navy,.002)
        box('Strata front dark face '+str(index),(0,-1.148,z),(.192,.040,.174),navy,.008)
        ring('Strata ceramic muzzle bezel '+str(index),-1.176,z,.067,.046,.020,white,40)
        ring('Strata titanium muzzle rim '+str(index),-1.188,z,.053,.044,.008,metal,40)
        ring('Strata recessed bore '+str(index),-1.162,z,.045,.038,.033,dark,40)
        api['cyl']('Strata bore shadow '+str(index),(0,-1.143,z),.038,.004,dark,32)

    # Long planar ceramic cheeks replace the previous inflated Orbital shells.
    for side in (-1,1):
        panel('Strata long upper fairing',side*.111,[(-1.09,.449),(-1.035,.473),(-.54,.476),(-.18,.445),(.102,.401),(.126,.333),(-.125,.330),(-.32,.365),(-1.07,.365)],.018,white)
        panel('Strata lower receiver cheek',side*.112,[(-.98,.245),(-.83,.272),(-.46,.270),(-.15,.235),(.10,.200),(.118,.035),(-.06,-.003),(-.35,.090),(-.50,.125),(-.96,.125)],.019,white)
        panel('Strata continuous grip transition',side*.095,[(-.29,.20),(.058,.19),(.099,.055),(.055,-.116),(-.015,-.153),(-.075,-.06),(-.20,-.005)],.014,white)
        panel('Strata oxblood inset',side*.124,[(-.30,.286),(-.15,.281),(-.124,.254),(-.30,.256)],.006,oxblood)
        box('Strata side status', (side*.132,-.195,.268),(.005,.055,.007),light,.002)
        for n in range(7):
            y=-.84+n*.065
            box('Strata recessed cooling vent',(side*.126,y,.414),(.005,.032,.010),dark,.002)
            box('Strata vent inner metal',(side*.130,y+.008,.414),(.003,.004,.009),metal,.001)
        for y,z in [(-.97,.391),(-.64,.446),(-.27,.400),(.034,.340),(-.37,.162)]:
            screw('Strata ceramic fastener',side,y,z,.126,.006)
        marking('STRATA',(side*.133,-.15,.355),.014,side)
        marking('02',(side*.135,-.195,.333),.012,side)

    # Receiver rear is closed in first person; no muzzle-like cutout faces the eye.
    box('Strata closed rear receiver',(0,.128,.224),(.210,.052,.274),navy,.020)
    box('Strata rear ceramic brow',(0,.145,.356),(.196,.031,.043),white,.007)
    for side in (-1,1):
        box('Strata rear ceramic return',(side*.097,.146,.215),(.023,.026,.170),white,.006)
    box('Strata rear status',(0,.157,.326),(.055,.004,.007),oxblood,.002)
    box('Strata upper rail',(0,-.50,.490),(.062,.99,.018),navy,.004)
    for i in range(11):
        box('Strata machined rail tooth',(0,-.91+i*.078,.503),(.061,.009,.011),metal,.002)
    box('Strata rear sight',(0,.056,.513),(.09,.064,.028),navy,.006)
    box('Strata front sight',(0,-1.01,.508),(.062,.023,.033),navy,.003)
    return join(parts[:],'weapon:joined')
