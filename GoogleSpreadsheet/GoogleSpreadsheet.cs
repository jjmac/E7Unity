using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Google.Apis.Auth.OAuth2;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Services;

using System.Security.Cryptography.X509Certificates;

using UnityEngine;
using UnityEditor;

class GoogleSpreadsheet
{

    private SheetsService service;

    public GoogleSpreadsheet(string p12PathFromAsset, string serviceAccountEmail)
    {
        var certificate = new X509Certificate2(Application.dataPath + Path.DirectorySeparatorChar + p12PathFromAsset, "notasecret", X509KeyStorageFlags.Exportable);

        ServiceAccountCredential credential = new ServiceAccountCredential(
           new ServiceAccountCredential.Initializer(serviceAccountEmail)
           {
               Scopes = new[] { SheetsService.Scope.SpreadsheetsReadonly } 
               /*
                Without this scope, it will :

                GoogleApiException: Google.Apis.Requests.RequestError
                Request had invalid authentication credentials. Expected OAuth 2 access token, login cookie or other valid authentication credential.

                lol..
                */
           }.FromCertificate(certificate));


        service = new SheetsService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
        });
    }

    public IList<IList<object>> Get(string spreadsheetId, string sheetNameAndRange)
    {
        SpreadsheetsResource.ValuesResource.GetRequest request = service.Spreadsheets.Values.Get(spreadsheetId, sheetNameAndRange);

        StringBuilder sb = new StringBuilder();

        ValueRange response = request.Execute();
        IList<IList<object>> values = response.Values;
        return values;
    }
}