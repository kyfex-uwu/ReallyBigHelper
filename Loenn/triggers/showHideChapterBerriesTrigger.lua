return {
    name="ReallyBigHelper/ShowHideChapterBerriesTrigger",
    placements={
        {
            name = "main",
            data = {
                mode="set",
                list="",
                onEnter=true
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
        }
    },
}