construction-step-condition-no-entity-in-tile = Can't be built on the same tile as a structure with { $requireAll ->
        [true] all
       *[false] any
    } of the following.{ $componentCount ->
        [0] {""}
       *[other] {" "}Components: { $components }.
    }{ $tagCount ->
        [0] {""}
       *[other] {" "}Tags: { $tags }.
    }{ $sizeCount ->
        [0] {""}
       *[other] {" "}Sizes: { $sizes }.
    }{ $toolQualityCount ->
        [0] {""}
       *[other] {" "}Tool qualities: { $toolQualities }.
    }
construction-step-condition-no-grille-in-tile = It can't be built under a grille.
anchorable-tile-blacklisted = Something on this tile is in the way.
