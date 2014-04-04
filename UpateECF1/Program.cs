using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UpateECF1.PSS.Project;
using UpateECF1.PSS.CustomFields;
using PSLibrary = Microsoft.Office.Project.Server.Library;
using System.Net;
using System.Security.Principal;
using System.Data;
using System.Data.SqlClient;
using System.Web.Services.Protocols;


namespace UpateECF1
{
    class Program
    {

        //define global variables
        public static List<Guid> ProjectGUIDs = new List<Guid>();

        public static string connectionString = "Data Source=PROJECTSERVERSQL;Initial Catalog=ProjectServer_Reporting;Integrated Security=True";//prod env
        
        public static string connectionStringCustom = "Data Source=PROJECTSERVERSQL;Initial Catalog=DBProjectServerCustom;Integrated Security=True";//prod env
        public static string strSelectProjUID = @"SELECT [ProjectUID] FROM [ProjectServer_Reporting].[dbo].[MSP_EpmProject_UserView] where [ITS Project ID] is null and projectcreateddate  > '08/18/2013'   order by  projectcreateddate"; //Created a new ECF Project ID. Date has been set to be greater than Time of Creation of Program, so that only values greater than this will be affected. Previous values will not be modified as per requirements.
        //public static string strSelectProjUID = @"SELECT top 3 [ProjectUID] FROM [ProjectServer_Reporting].[dbo].[MSP_EpmProject_UserView] where [ITS Proj ID] = 400 order by  projectcreateddate";
        public static string strSelectProjID =  @"SELECT [ProjID] FROM [tb_projectID_Counter]";
        public static string strInsertProjID =  @"update top (1) [tb_projectID_Counter] set [ProjID] = @ProjID";
        public static string strCustomFieldsWS = "customfields.asmx";
        public static string loginName = "pmospfarmadmin"; // whatever your login name is, this is just a sample
        public static string password = "MYPASSWORD";//whatever your password is, this is just a sample
        public static Guid myCustomFieldId = new Guid("{7688eefc-b8b2-4dba-86e3-9639f82286dd}");// ITS Project ID prod
  
 
        //public static Guid myCustomFieldId = new Guid("{1917f585-2cf9-47a1-aed0-c00853c2c184}");//IDProjID for test
        public static string MD_PROP_NAME = "MD_PROP_NAME";
        //public static string strProjID2 = "ITS Proj ID";
        public static string strProjectID = "ITS Project ID";
        public static string MD_PROP_UID = "MD_PROP_UID";
        public static string MD_PROP_ID = "MD_PROP_ID";
        // Set the value of the PWA instances: one that has Windows authentication only,
        // and one that has multi-authentication (Windows and Forms).


       
        private const string PROJECTSERVER_WIN_URL = "http://mypmoweb/PMO/_vti_bin/psi/"; //live env
        private static string projectServerUrl;


        //gets all the project guids where ProjID ECF is null
        public static void GetProjectGUIDs()
        {

            DataTable dt = new DataTable();

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                con.Open();

                using (SqlCommand command = new SqlCommand (strSelectProjUID,con))
                {
                    
                    using (SqlDataAdapter da = new SqlDataAdapter(command))
                    {
                        da.Fill(dt);
                        foreach(DataRow dr in dt.Rows)
                        {
                                ProjectGUIDs.Add((Guid)dr[0]);
                        }
                    }
                }
            }

        }


        /// <summary>
        /// gets the beginning count values for proj id that will be used to give to project with null ITS_Proj_ID field
        /// </summary>
        /// <returns></returns>
        public static int GetIDCount()
        {
            DataTable dt = new DataTable();
            int ProjCount = 0;

            using (SqlConnection con = new SqlConnection(connectionStringCustom))
            {
                con.Open();

                using (SqlCommand command = new SqlCommand(strSelectProjID, con))
                {
                    ProjCount = (int)command.ExecuteScalar();
                }
            }
            //Next Counter shoudld be one more that what we have in db.
            ProjCount++;
            return ProjCount;
        }

        /// <summary>
        /// sets the ending count values for proj id for ITS_Proj_ID field after all the projID have been allocated.
        /// </summary>
        /// <returns></returns>
        public static void SetIDCount(int currentcount)
        {

            using (SqlConnection con = new SqlConnection(connectionStringCustom))
            {
                con.Open();

                using (SqlCommand command = new SqlCommand(strInsertProjID, con))
                {
                    command.Parameters.AddWithValue("@ProjID", currentcount);
                    command.ExecuteNonQuery();
                }
            }
        }
        
        
        /// <summary>
        /// updates custom field
        /// </summary>
        public static void UpdateCustomField()
        {
            //Creating a new service client object
            ProjectSoapClient projectSvc = new ProjectSoapClient();
            


            bool isWindowsUser = true;
            projectServerUrl = PROJECTSERVER_WIN_URL;


            //Creating a new service client object
            CustomFieldDerived CustomFieldsSvc = new CustomFieldDerived();
            CustomFieldsSvc.Url = projectServerUrl + strCustomFieldsWS;
            CustomFieldsSvc.Credentials = CredentialCache.DefaultCredentials;

            CustomFieldsSvc.CookieContainer = GetLogonCookie();
            CustomFieldsSvc.EnforceWindowsAuth = isWindowsUser;
           
            
            //Just if you need to authenticate with another account
            projectSvc.ClientCredentials.Windows.ClientCredential = new NetworkCredential(loginName, password);
            projectSvc.ClientCredentials.Windows.AllowedImpersonationLevel = TokenImpersonationLevel.Impersonation;

            
            //CustomFieldsSvc.ClientCredentials.Windows.AllowedImpersonationLevel = TokenImpersonationLevel.Impersonation;

            
            //Guid of my project
            Guid myProjectId = new Guid();
            



            //creating a new sessionId and a new jobId
            Guid sessionId = Guid.NewGuid(); //the sessionId stays for the whole updating process
            Guid jobId = Guid.NewGuid(); //for each job, you give to the server, you need a new one

            //indicator if you have to update the project
            Boolean updatedata = false;

          
            //get proj count from database
            int currentCount = GetIDCount();
           // int currentCount = 1; testing only
            

            //loading project data from server
            //Every change on this dataset will be updated on the server!
            for (int i = 0; i < ProjectGUIDs.Count; i++)
            {
                try
                {
                    myProjectId = ProjectGUIDs[i];
                    ProjectDataSet project = projectSvc.ReadProject(myProjectId, DataStoreEnum.WorkingStore);

                    if (project != null)
                    {
                    Console.WriteLine("Adding Proj ID's to ProjectGUID: " + myProjectId.ToString());
                    ProjectDataSet.ProjectCustomFieldsDataTable P_dt = project.ProjectCustomFields;//*****This Line Represents the Assigned fields are present in Project.
                    CustomFieldDataSet CstmDS = CustomFieldsSvc.ReadCustomFieldsByEntity(new Guid(PSLibrary.EntityCollection.Entities.ProjectEntity.UniqueId.ToString()));


                    updatedata = false;

                    foreach (DataRow row in CstmDS.CustomFields)
                    {
                        //This is Normal project Text field.
                        if ((String)row[MD_PROP_NAME] == strProjectID)
                        {
                            ProjectDataSet.ProjectCustomFieldsRow rowProjCF = P_dt.NewProjectCustomFieldsRow();
                            rowProjCF.MD_PROP_UID = (Guid)row[MD_PROP_UID];
                            rowProjCF.PROJ_UID = myProjectId;
                            rowProjCF.MD_PROP_ID = (int)row[MD_PROP_ID];
                            rowProjCF.CUSTOM_FIELD_UID = Guid.NewGuid();
                            rowProjCF.TEXT_VALUE = currentCount.ToString();
                            

                            project.ProjectCustomFields.AddProjectCustomFieldsRow(rowProjCF);

                            //and set the indicater
                            updatedata = true;
                            //increment current counter
                            currentCount++;
                            break;
                        }
                    }



                        
                        

                        //update if you have changed anything
                        if (updatedata)
                        {
                            //check out the project first
                            projectSvc.CheckOutProject(myProjectId, sessionId, "custom field update checkout");

                            //send the dataset to the server to update the database
                            bool validateOnly = false;
                            
                            projectSvc.QueueUpdateProject(jobId, sessionId, project, validateOnly);
                            

                            //wait 4 seconds just to be sure the job has been done
                            System.Threading.Thread.Sleep(4000);

                            //create a new jobId to check in the project
                            jobId = Guid.NewGuid();

                            //CheckIn
                            bool force = false;
                            string sessionDescription = "updated custom fields";
                            projectSvc.QueueCheckInProject(jobId, myProjectId, force, sessionId, sessionDescription);

                            //wait again 4 seconds
                            System.Threading.Thread.Sleep(4000);

                            /* now the data is in the database
                             * but the gui wont display the new value
                             * so we have to publish the project to display
                             * the new data.
                             * 
                             * Maybe this is weird, but it works for me.
                             */

                            //again a new jobId to publish the project
                            jobId = Guid.NewGuid();
                            bool fullPublish = true;
                            projectSvc.QueuePublish(jobId, myProjectId, fullPublish, null);

                            //maybe we should wait again
                            //System.Threading.Thread.Sleep(4000);
                        }

                    }
                    else
                        Console.WriteLine("Project dataset was returned null for ProjectGUID: " + myProjectId.ToString());
                }
                //catching an exception in a for loop since we would not want to break off if there is an error on one project. Maybe a manual force checkin would be required for such projects
                catch (Exception ex) //use SoapException while debugging. See unused code at the bottom
                {
                 
                    Console.WriteLine(" Error encountered: " + ex.ToString());
                    Console.WriteLine();
                    Console.WriteLine(" Program execution will continue: " + ex.ToString());
                    System.IO.File.WriteAllText(@"C:\UpdateECFLogs.txt", ex.ToString());

                }

               
            }
            //deduct the counter by 1 to improvise for the last iteration increment of the counter in the for loop.
            currentCount--;
            //send the current count value to the db.
            SetIDCount(currentCount);
            currentCount = 0;
            //clear projectguid when done
            ProjectGUIDs.Clear();
            //clear projectguid's
            myProjectId = new Guid();
            
            
           
        }

        // Derive from the Project class; add an additional property that specifies 
        // whether to enforce Windows authentication, and then override the Web request header
        // in multi-authentication installations.
        class CustomFieldDerived : PSS.CustomFields.CustomFields
        {
            public bool EnforceWindowsAuth { get; set; }

            protected override WebRequest GetWebRequest(Uri uri)
            {
                WebRequest request = base.GetWebRequest(uri);

                if (this.EnforceWindowsAuth)
                {
                    request.Headers.Add("X-FORMS_BASED_AUTH_ACCEPTED", "f");
                }
                return request;
            }
        }


      

        
        /// <summary>
        /// Get a CookieContainer property from the derived LoginWindows object.
        /// </summary>
        /// <returns></returns>
        private static CookieContainer GetLogonCookie()
        {
            // Create an instance of the loginWindows object.
            LoginWindowsDerived loginWindows = new LoginWindowsDerived();
            loginWindows.EnforceWindowsAuth = true;
            loginWindows.Url = projectServerUrl + "LoginWindows.asmx";
            loginWindows.Credentials = CredentialCache.DefaultCredentials;

            loginWindows.CookieContainer = new CookieContainer();

            if (!loginWindows.Login())
            {
                // Login failed; throw an exception.
                throw new UnauthorizedAccessException("Login failed.");
            }
            return loginWindows.CookieContainer;
        }


        // Derive from the LoginWindows class; add an additional property that specifies 
        // whether to enforce Windows authentication, and then override the Web request header.
        class LoginWindowsDerived : LoginWindowsWebSvc.LoginWindows
        {
            public bool EnforceWindowsAuth { get; set; }

            protected override WebRequest GetWebRequest(Uri uri)
            {
                WebRequest request = base.GetWebRequest(uri);

                if (this.EnforceWindowsAuth)
                {
                    request.Headers.Add("X-FORMS_BASED_AUTH_ACCEPTED", "f");
                }
                return request;
            }
        }



        
        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine(" This program will automatically allot sequential Project ID's to the recently added project and the one which don't have them");
                Console.WriteLine("Begin Execution......");

                //get projectGUID's to update
                Console.WriteLine("Retrieving projects to add Proj ID's. Contacting Reporting database..");
                GetProjectGUIDs();
                Console.WriteLine("Found " + ProjectGUIDs.Count + " projects to add Proj Id's to.");
                //update custom fields
                UpdateCustomField();
                Console.WriteLine(" Execution completed successfully. Project ID's were added to all the projects in the queue.");
                Console.WriteLine(" Program will now shut down in 10 seconds...");
                System.Threading.Thread.Sleep(10000);
                
            }
            catch (Exception ex)
            {
                
                Console.WriteLine(" Fatal error: " + ex.ToString());
                Console.WriteLine(" Program execution has been terminated. Email has been sent to the administrators. ");
                System.IO.File.WriteAllText(@"C:\UpdateECFLogs.txt", ex.ToString());
                System.Threading.Thread.Sleep(10000);
            }
        }
    }
}



//unused code
//Piece of code to catch soapexception
//string errAttributeName;
//string errAttribute;
//string errMess = "".PadRight(30, '=') + "\r\n" + "Error: " + "\r\n";

//PSLibrary.PSClientError error = new PSLibrary.PSClientError(ex);
//PSLibrary.PSErrorInfo[] errors = error.GetAllErrors();
//PSLibrary.PSErrorInfo thisError;

//for (int z = 0; z < errors.Length; z++)
//{
//    thisError = errors[z];
//    errMess += "\n" + ex.Message.ToString() + "\r\n";
//    errMess += "".PadRight(30, '=') + "\r\nPSCLientError Output:\r\n \r\n";
//    errMess += thisError.ErrId.ToString() + "\n";

//    for (int j = 0; j < thisError.ErrorAttributes.Length; j++)
//    {
//        errAttributeName = thisError.ErrorAttributeNames()[j];
//        errAttribute = thisError.ErrorAttributes[j];
//        errMess += "\r\n\t" + errAttributeName +
//                   ": " + errAttribute;
//    }
//    errMess += "\r\n".PadRight(30, '=');
//}

/// <summary>
/// creates custom field for ProjId
/// </summary>
//    public static CustomFieldDataSet.CustomFieldsRow CreateCustomField()
//    {
//        CustomFieldDerived CustomFieldsSvc = new CustomFieldDerived();
//        CustomFieldDataSet rowCustomField = new CustomFieldDataSet();
//        CustomFieldDataSet.CustomFieldsRow cfRow = rowCustomField.CustomFields.NewCustomFieldsRow();
//        Guid cfUid = Guid.NewGuid();

//        string cfName = "Test Task Cost";
//        Guid entityTypeUid = new Guid(PSLibrary.EntityCollection.Entities.TaskEntity.UniqueId);

//        Guid lookupTableUid = Guid.Empty;
//        Guid ltRowDefaultUid = Guid.Empty;
//        byte cfType = (byte)PSLibrary.CustomField.Type.COST;
//        byte rollup = (byte)PSLibrary.CustomField.SummaryRollup.Sum;



//            cfRow.MD_PROP_UID = cfUid;
//cfRow.MD_AGGREGATION_TYPE_ENUM = rollup;
//cfRow.MD_ENT_TYPE_UID = entityTypeUid;
//cfRow.MD_PROP_NAME = cfName;
//cfRow.MD_PROP_IS_REQUIRED = false;
//cfRow.MD_PROP_IS_LEAF_NODE_ONLY = false;
//cfRow.MD_PROP_TYPE_ENUM = cfType;

//if (lookupTableUid == Guid.Empty)
//    cfRow.SetMD_LOOKUP_TABLE_UIDNull();
//else
//    cfRow.MD_LOOKUP_TABLE_UID = lookupTableUid;

//if (ltRowDefaultUid == Guid.Empty)
//    cfRow.SetMD_PROP_DEFAULT_VALUENull();
//else
//    cfRow.MD_PROP_DEFAULT_VALUE = ltRowDefaultUid;

//rowCustomField.CustomFields.Rows.Add(cfRow);

//try
//{
//    bool validateOnly = false;
//    bool autoCheckIn = true;
//    CustomFieldsSvc.CreateCustomFields(rowCustomField, validateOnly, autoCheckIn);
//}
//catch (Exception ex) // not using soap exception for now
//{
//    // Add exception handler for ex.
//    cfUid = Guid.Empty;
//}
//        //return cfUid;
//return cfRow;


//    }

//To find your custom field, you have to search for it in the CustomFieldsRow
//foreach (UpateECF1.PSS.Project.ProjectDataSet.ProjectCustomFieldsRow row in project.ProjectCustomFields)
//{
//    //check if the GUID is the same

//    if (row.MD_PROP_UID == myCustomFieldId)
//    {
//        //if yes, write it into the container
//        row.NUM_VALUE = currentCount;

//        //and set the indicater
//        updatedata = true;

//        //increment current counter
//        currentCount++;

//        break;
//    }
//}
