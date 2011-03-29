﻿using System;
using TaskLeader.DAL;
using System.Configuration;
using System.IO;
using System.Windows.Forms;
using TaskLeader.GUI;

namespace TaskLeader.BLL
{
    public class Init
    {
        // Variable locale pour stocker une référence vers l'instance
        private static Init v_instance = null;

        // Renvoie l'instance ou la crée
        public static Init Instance
        {
            get
            {
                // Si pas d'instance existante on en crée une...
                if (v_instance == null)
                    v_instance = new Init();

                // On retourne l'instance de MonSingleton
                return v_instance;
            }
        }

        public bool canLaunch()
        {
            // Vérification de la présence du fichier de base
            if (File.Exists(ConfigurationManager.AppSettings["cheminDB"]))
            {
                // On vérifie la nécessité d'une migration de la base
                return checkMigration();
            }
            else
            {
                // La base n'est pas accessible à l'adresse indiquée
                MessageBox.Show("Fichier base introuvable\nVérifier fichier de conf", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return false;  
            }
        }

        // Migration
        private bool checkMigration()
        {
            // On vérifie que la version de la GUI est bien dans la base
            bool baseCompatible = ReadDB.Instance.isVersionComp(Application.ProductVersion.Substring(0, 3));

            if (!baseCompatible)
            {
                if (ReadDB.Instance.getLastVerComp() == "0.4.0.0")
                {
                    TrayIcon.afficheMessage("Migration", "La base est obsolète, migration en cours");
                    migration("04-06");
                    return true;
                }
                else if (ReadDB.Instance.getLastVerComp() == "0.5")
                {
                    TrayIcon.afficheMessage("Migration", "La base est obsolète, migration en cours");
                    migration("05-06");
                    return true;
                }
                else
                {
                    MessageBox.Show("La base est trop ancienne pour une migration", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return false;
                }         
            }
            else
                return true; // La base est compatible, rien à faire.
        }

        private void migration(String change)
        {
            // Copie de sauvegarde du fichier db avant toute manip
            TrayIcon.afficheMessage("Migration","Copie de sauvegarde de la base");
            String sourceFile = ConfigurationManager.AppSettings["cheminDB"];
            String backupFile = sourceFile.Substring(0, sourceFile.Length - 4) + DateTime.Now.ToString("_Back-ddMMyyyy") + ".db3";
            System.IO.File.Copy(sourceFile, backupFile,true);

            // Récupération du script de migration
            try
            {
                String script = System.IO.File.ReadAllText(@"../Migration/" + change + ".sql", System.Text.Encoding.UTF8);

                // Exécution du script
                TrayIcon.afficheMessage("Migration", "Exécution du script de migration");
                WriteDB.Instance.execSQL(script);

                // Nettoyage de la base
                WriteDB.Instance.execSQL("VACUUM;");
                TrayIcon.afficheMessage("Migration", "Migration de la base effectuée");
            }
            catch
            {
                //TODO:affiner le pourquoi
                TrayIcon.afficheMessage("Migration", "Fichier de migration introuvable");
            }           
        }
    }
}
