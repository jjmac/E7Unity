using Firebase;
using Firebase.Storage;
using Firebase.Database;
using Firebase.Auth;
using System;
using System.Threading.Tasks;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using Firebase.Unity.Editor;
#endif

/// <summary>
/// A small toolkit I made for common operations.
/// Currently it can download-upload from persistent data path. 
/// </summary>
/// <returns></returns>
public abstract class FirebaseToolkit<ITSELF> where ITSELF : FirebaseToolkit<ITSELF>, new()
{
    /// <summary>
    /// Format : "gs://my-custom-bucket"
    /// </summary>
    protected abstract string BucketName { get; }

    /// <summary>
    /// Format : "https://yourgame.firebaseio.com/"
    /// </summary>
    protected abstract string DatabaseUrl { get; }

    /// <summary>
    /// Format : "askfjsdafkj.p12"
    /// </summary>
    protected abstract string P12FileName { get; }

    /// <summary>
    /// Format : "firebase-adminsdk-mpvab@your-game.iam.gserviceaccount.com"
    /// </summary>
    protected abstract string ServiceAccountEmail { get; }

    //All the C# abstract cannot be static so those are useless to the abstract methods.. because of this we have to make a singleton of FirebaseToolkit just for accessing those abstracts. lol
    //But this is internal use singleton, so it is private.
    protected static ITSELF instance;
    protected static ITSELF Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new ITSELF();
            }
            return instance;
        }
    }

    private static FirebaseAuth auth;
    protected static FirebaseAuth Auth 
    { 
        get 
        {
            if(auth == null)
            {
                auth = FirebaseAuth.DefaultInstance;
            }
            return auth;
        }
    }

    protected static bool IsLoggedIn
    {
        get{
#if UNITY_EDITOR
            if(
                FirebaseApp.DefaultInstance.GetEditorServiceAccountEmail() != "" &&
                FirebaseApp.DefaultInstance.GetEditorAuthUserId() != "" &&
                FirebaseApp.DefaultInstance.GetEditorAuthUserId() != null)
            {
                return true;
            }
            else
            {
                return false;
            }
#else
            return (Auth.CurrentUser != null && Auth.CurrentUser.UserId != "");
#endif
        }
    }

    protected static string UserEmail
    {
        get{
            if(IsLoggedIn== false)
            {
                throw new Exception("Cannot get E-mail while not logged in");
            }
#if UNITY_EDITOR
            return FirebaseApp.DefaultInstance.GetEditorServiceAccountEmail();
#else
            return Auth.CurrentUser.Email;
#endif
        }
    }

	protected static string UserID 
	{
		get
		{
            if(IsLoggedIn == false)
            {
                throw new Exception("Cannot get ID while not logged in");
            }
#if UNITY_EDITOR
            return FirebaseApp.DefaultInstance.GetEditorAuthUserId();
#else
            return Auth.CurrentUser.UserId;
#endif
		}
	}

    private static FirebaseDatabase database;
    public static FirebaseDatabase Database 
    { 
        get 
        {
            if(database == null)
            {
#if UNITY_EDITOR
                FirebaseApp.DefaultInstance.SetEditorDatabaseUrl(Instance.DatabaseUrl);
                database = FirebaseDatabase.DefaultInstance;
#else
                database = FirebaseDatabase.GetInstance(Instance.DatabaseUrl);
#endif
            }
            return database;
        }
    }

    private static FirebaseStorage storage;
    public static FirebaseStorage Storage
    {
        get
        {
            if (storage == null)
            {
                storage = FirebaseStorage.GetInstance(Instance.BucketName);
            }
            return storage;
        }
    }

    public static void LogCurrentUser()
    {
        if(IsLoggedIn)
        {
            Debug.Log(string.Format("UID : {0} Email : {1}", UserID, UserEmail));
        }
        else
        {
            Debug.Log("Currently not logged in..");
        }
    }

    /// <summary>
    /// You cannot store files in Firebase root. Folder argument is a must.
    /// </summary>
    public static void UploadFromPersistent(string localFileName, string folder, string destinationFileName, Action<Task<StorageMetadata>> onSuccess = null, Action<Task<StorageMetadata>> onFailure = null)
    {
        StorageReference uploadReference = Storage.RootReference.Child(folder).Child(destinationFileName);

        FileStream stream = new FileStream(Application.persistentDataPath + Path.DirectorySeparatorChar + localFileName, FileMode.Open, FileAccess.Read, FileShare.Read);
        uploadReference.PutStreamAsync(stream).ContinueWith((Task<StorageMetadata> task) =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                if (onFailure == null)
                {
                    DefaultFailure("Upload " + localFileName, task);
                }
                else
                {
                    onFailure.Invoke(task);
                }
            }
            else
            {
                if (onSuccess == null)
                {
                    DefaultUploadSuccess(task);
                }
                else
                {
                    onSuccess.Invoke(task);
                }
            }
            //Cannot use "using" since it is based on callback. The stream will already be disposed by then.
            stream.Dispose(); 
        });

    }

    private static void DefaultFailure(string fileName, Task task)
    {
        Debug.LogError("Firebase Failure : " + fileName);
        Debug.LogError(task.Exception.ToString());
    }

    private static void DefaultUploadSuccess(Task<StorageMetadata> task)
    {
        Debug.Log("Firebase Upload Success : " + task.Result.Name);
    }

    private static void DefaultDownloadSuccess(string fileName, Task task)
    {
        Debug.Log("Firebase Download Success : " + fileName);
    }

    public static void DownloadToPersistent(string folder, string storageFileName, string saveFileName, Action<Task> onSuccess = null, Action<Task> onFailure = null)
    {
        StorageReference downloadReference = Storage.RootReference.Child(folder).Child(storageFileName);
        downloadReference.GetFileAsync(Application.persistentDataPath + Path.DirectorySeparatorChar + saveFileName).ContinueWith(
(Task task) =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                if (onFailure == null)
                {
                    DefaultFailure("Download " + downloadReference.Path, task);
                }
                else
                {
                    onFailure.Invoke(task);
                }
            }
            else
            {
                if (onSuccess == null)
                {
                    DefaultDownloadSuccess("Download " + downloadReference.Path, task);
                }
                else
                {
                    onSuccess.Invoke(task);

                }
            }
        }
        );
    }

}