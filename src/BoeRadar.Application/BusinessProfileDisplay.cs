namespace BoeRadar.Application;

public static class BusinessProfileDisplay
{
    public static string Describe(BusinessProfile profile)
    {
        if (!BusinessProfileMatcher.IsValid(profile)) throw new ArgumentException("Perfil no válido.");
        var activity = profile.Activity switch
        {
            "retail" => "Comercio",
            "hospitality" => "Hostelería y turismo",
            "construction" => "Construcción y rehabilitación",
            "technology" => "Tecnología e innovación",
            "professional" => "Servicios profesionales",
            "agriculture" => "Agricultura, ganadería y pesca",
            "transport" => "Transporte y movilidad",
            "educationSport" => "Formación y deporte",
            _ => "Otra actividad / varias actividades"
        };
        var territory = profile.Territory switch
        {
            "all" => "Varios territorios / sin especificar",
            "andalucia" => "Andalucía",
            "aragon" => "Aragón",
            "asturias" => "Asturias",
            "baleares" => "Illes Balears",
            "canarias" => "Canarias",
            "cantabria" => "Cantabria",
            "castillaLaMancha" => "Castilla-La Mancha",
            "castillaLeon" => "Castilla y León",
            "cataluna" => "Cataluña",
            "valencia" => "Comunitat Valenciana",
            "extremadura" => "Extremadura",
            "galicia" => "Galicia",
            "madrid" => "Comunidad de Madrid",
            "murcia" => "Región de Murcia",
            "navarra" => "Navarra",
            "paisVasco" => "País Vasco",
            "laRioja" => "La Rioja",
            "ceuta" => "Ceuta",
            "melilla" => "Melilla",
            _ => throw new ArgumentException("Territorio no válido.")
        };
        return $"{(profile.BusinessType == "sme" ? "Pyme" : "Autónomo/a")} · {activity} · {territory}";
    }
}
