UpdateECFProjectServer
======================

 Code Name: Update ECF 

Introduction: 
This app code can run on the Sharepoint 3.0 server with Project Server 2010 installed, every 30 min or regularly (depending on the need) with the Task Name ‘ProjectID Populator’. It will log errors to “C:/UpdateLogs.txt” 
Databases Used: 
•	 ProjectServer_Reporting : Views - [MSP_EpmProject_UserView] (Do not make any modification to this view)
•	 DBProjectServerCustom (schematically mirrored on test and prod): Tables: [tb_projectID_Counter] – If project id counter needs to be reset to restarted from a certain count, it can be done here. 
Please make sure that when modifying the code, add, WCF as ‘Add Web Reference’. Service References can be added as is
WCF used: 
•	http://mypmoweb/PMO/_vti_bin/PSI/LoginWindows.asmx?wsdl
•	http://mypmoweb/PMO/ _vti_bin/PSI/customfields.asmx?wsdl
Service References Used:
•	http://mypmoweb/PMO/_vti_bin/PSI/Project.asmx?wsdl


Code Summary:
•	Gets Project ID from the custom table - tb_projectID_Counter to base off the count from.
•	Gets all the project from reporting server whose ProjectGUID is Null and Project creation date is greater than the date this console starts running
•	Checks out the project after passing authentication (pmospafarmadmin)
•	Updates the ITS PROJECT ID (a custom field name) on all and send them to the queue for update.
•	CheckIn the project.
•	Adds it the Project Queue
•	When all the new projects are checked in, it updates the counter on custom table so that the next time the counter runs; it starts from the current count.
•	If there is exceptiom or error, it logs it to text file


Troubleshooting:
•	If program skips some projects altogether and never allots them Project ID’s then make sure that the project is not in checked out state. Do a force check in if necessary.
•	If Program runs successfully without incrementing, numerous steps will need to be checked including the following (the best way to do isolate the causes is the debug the app, check the errors):
o	Change in ECF – If ECF’s are dropped and recreated, it will change their GUID’s so the program will be not work.
o	Sharepoint WCF or web service can be down, unreachable 
•	Task hangs: Perform above two steps and restart the task.

