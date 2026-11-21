return {
    name="ReallyBigHelper/ShowHideChapterBerriesTrigger",
    placements={
        {
            name = "main",
            data = {
                mode="set",
                list="",
                onEnter=true,
                forceShow="unset",
            },
        }
    },
    fieldInformation = {
        mode = {
            options = {
                "add",
                "remove",
                "set",
                "reset"
            },
            editable=false,
        },
        list = {
            fieldType = "list",
            elementDefault = "0",
        },
        forceShow = {
            default="unset"
            options={
                "show",
                "hide",
                "unset",
                "vanilla",
            }
        }
    },
    fieldOrder={
        "x", "y",
        "width","height",
        "list",
        "mode",
        "onEnter",
        "forceShow",
    },
}