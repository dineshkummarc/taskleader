﻿using System;
using System.Configuration;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using TaskLeader.DAL;
using TaskLeader.GUI;

namespace TaskLeader.BO
{
    public enum EncStatus
    {
        /// <summary>
        /// New enclosure added to the list
        /// </summary>
        added,
        /// <summary>
        /// Original enclosure removed from the list
        /// </summary>
        removed,
        /// <summary>
        /// Original enclosure renamed
        /// </summary>
        renamed, //TODO:Plutôt modified
        /// <summary>
        /// Original enclosure
        /// </summary>
        original,
        /// <summary>
        /// New enclosure deleted at the end
        /// </summary>
        bin
    }

    public class EncWithStatus
    {
        public Enclosure enclosure;
        public EncStatus status;
        public EncWithStatus(Enclosure enc, EncStatus stat) { enclosure = enc; status = stat; }
    }

    public class TLaction
    {
        // Méthode privée pour fabriquer des string compatible sql
        private String sqlFactory(String original) { return "'" + original.Replace("'", "''") + "'"; }

        // Membre privé permettant de détecter des updates
        private bool initialStateFrozen = false;

        // DB d'où provient l'action
        public String dbName = TrayIcon.defaultDB.name;
        private DB db { get { return TrayIcon.dbs[this.dbName]; } }

        // ID de l'action dans la base TaskLeader
        private String v_TLID = "";
        public String ID { get { return v_TLID; } }
        public bool isScratchpad { get { return (v_TLID == ""); } }

        #region Contexte de l'action

        private String v_ctxt = TrayIcon.defaultDB.getDefault(DB.contexte);
        public bool ctxtHasChanged = false;
        public String Contexte
        {
            get { return v_ctxt; }
            set
            {
                if (value != v_ctxt)
                {
                    ctxtHasChanged = this.initialStateFrozen;
                    v_ctxt = value;
                }
            }
        }
        public String ContexteSQL { get { return sqlFactory(v_ctxt); } }

        #endregion

        #region Sujet de l'action

        private String v_sujt = TrayIcon.defaultDB.getDefault(DB.sujet);
        public bool sujetHasChanged = false;
        public String Sujet
        {
            get { return v_sujt; }
            set
            {
                if (value != v_sujt)
                {
                    sujetHasChanged = this.initialStateFrozen;
                    v_sujt = value;
                }
            }
        }
        public String SujetSQL { get { return sqlFactory(v_sujt); } }

        #endregion

        #region Libéllé de l'action

        private String v_texte = "";
        public bool texteHasChanged = false;
        public String Texte
        {
            get { return v_texte; }
            set
            {
                if (value != v_texte)
                {
                    texteHasChanged = this.initialStateFrozen;
                    v_texte = value;
                }
            }
        }
        public String TexteSQL { get { return sqlFactory(v_texte); } }

        #endregion

        #region DueDate de l'action

        private DateTime v_dueDate = DateTime.MinValue;
        public bool dueDateHasChanged = false;
        public bool hasDueDate { get { return (v_dueDate != DateTime.MinValue); } }
        public DateTime DueDate
        {
            get { return v_dueDate; }
            set
            {
                if (value != v_dueDate)
                {
                    dueDateHasChanged = this.initialStateFrozen;
                    v_dueDate = value;
                }
            }
        }
        public void parseDueDate(String date) { DateTime.TryParse(date, out v_dueDate); }
        public String DueDateSQL { get { return "'" + v_dueDate.ToString("yyyy-MM-dd") + "'"; } }

        #endregion

        #region Destinataire de l'action

        private String v_dest = TrayIcon.defaultDB.getDefault(DB.destinataire);
        public bool destHasChanged = false;
        public String Destinataire
        {
            get { return v_dest; }
            set
            {
                if (value != v_dest)
                {
                    destHasChanged = this.initialStateFrozen;
                    v_dest = value;
                }
            }
        }
        public String DestinataireSQL { get { return sqlFactory(v_dest); } }

        #endregion

        #region Statut de l'action

        private String v_stat = TrayIcon.defaultDB.getDefault(DB.statut); // Le statut est initialisé avec la valeur par défaut
        public bool statusHasChanged = false;
        public String Statut
        {
            get { return v_stat; }
            set
            {
                if (value != v_stat)
                {
                    statusHasChanged = this.initialStateFrozen;
                    v_stat = value;
                }
            }
        }
        public String StatutSQL { get { return sqlFactory(v_stat); } }

        #endregion

        #region PJs de l'action

        private List<EncWithStatus> _links = new List<EncWithStatus>();
        public List<Enclosure> PJ { get { return _links.Select(ews => ews.enclosure).ToList<Enclosure>(); } }
        public bool hasPJ { get { return (_links.Count > 0); } }

        private void initAdd(List<Enclosure> list) { _links.AddRange(list.Select(enc => new EncWithStatus(enc, EncStatus.original))); }

        public int addPJ(Enclosure enc)
        {
            _links.Add(new EncWithStatus(enc, EncStatus.added));
            return _links.Count - 1;
        }

        public void removePJ(int encIndex)
        {
            switch (_links[encIndex].status)
            {
                case EncStatus.original:
                case EncStatus.renamed:
                    _links[encIndex].status = EncStatus.removed;
                    break;

                case EncStatus.added:
                    _links[encIndex].status = EncStatus.bin;
                    break;
            }
        }

        public void renamePJ(int encIndex, String newTitle)
        {
            _links[encIndex].enclosure.Titre = newTitle;

            if (_links[encIndex].status == EncStatus.original) // Dans tous les autres cas, on reste en 'renamed'
                _links[encIndex].status = EncStatus.renamed;
        }

        #endregion

        #region Constructeurs

        /// <summary>
        /// Constructeur permettant d'initialiser les valeurs par défaut
        /// </summary>
        public TLaction() { this.initialStateFrozen = true; }

        /// <summary>
        /// Constructeur à partir de l'ID de stockage de l'action
        /// </summary>
        /// <param name="ID">ID dans la base d'actions</param>
        /// <param name="database">Nom de la base d'actions</param>
        public TLaction(String ID, String database)
        {
            this.dbName = database;
            this.v_TLID = ID;

            //Récupération des données de l'action
            DataRow data = db.getAction(ID);

            this.v_ctxt = data["Contexte"] as String;
            this.v_sujt = data["Sujet"] as String;
            this.v_texte = data["Titre"] as String;
            this.parseDueDate(data["Deadline"] as String);
            this.v_dest = data["Destinataire"] as String;
            this.v_stat = data["Statut"] as String;

            //Récupération des liens
            this.initAdd(db.getPJ(ID));

            this.initialStateFrozen = true;
        }

        #endregion

        #region Mises à jour

        /// <summary>
        /// Mise à jour des champs principaux
        /// </summary>
        /// <param name="contexte">Nouveau contexte</param>
        /// <param name="subject">Nouveau sujet</param>
        /// <param name="desAction">Nouvelle description</param>
        /// <param name="destinataire">Nouveau destinataire</param>
        /// <param name="stat">Nouveau statut</param>
        public void updateDefault(String contexte, String subject, String desAction, String destinataire, String stat)
        {
            // Utilisation volontaire des attributs publics pour détecter les changements
            this.Contexte = contexte;
            this.Sujet = subject;
            this.Texte = desAction;
            this.Destinataire = destinataire;
            this.Statut = stat;
        }

        /// <summary>
        /// Change la base correspondant à l'action scratchpad
        /// </summary>
        /// <param name="nomDB">Nom de la nouvelle base</param>
        public void changeDB(String nomDB)
        {
            // Changement du nom de la base
            this.dbName = nomDB;

            // Changement des valeurs par défaut
            this.v_ctxt = TrayIcon.dbs[dbName].getDefault(DB.contexte);
            this.v_sujt = TrayIcon.dbs[dbName].getDefault(DB.sujet);
            this.v_dest = TrayIcon.dbs[dbName].getDefault(DB.destinataire);
            this.v_stat = TrayIcon.dbs[dbName].getDefault(DB.statut);
        }

        #endregion

        /// <summary>
        /// Sauvegarde de l'action dans la base correspondante
        /// </summary>
        public void save()
        {
            String bilan = "";
            int resultat;

            // On rajoute une ligne d'historique si le statut est différent de Ouverte et si le statut a changé
            if (this.Statut != db.getDefault(DB.statut) && this.statusHasChanged)
                this.Texte += Environment.NewLine + "Action " + this.Statut + " le: " + DateTime.Now.ToString("dd-MM-yyyy");

            // Vérification des nouveautés
            if (this.ctxtHasChanged) // Test uniquement si contexte entré
                if (db.isNvo(DB.contexte, this.Contexte)) // Si on a un nouveau contexte
                {
                    resultat = db.insert(DB.contexte, this.Contexte); // On récupère le nombre de lignes insérées
                    if (resultat == 1)
                        bilan += "Nouveau contexte enregistré\n";
                }

            if (this.sujetHasChanged)
                if (db.isNvoSujet(this.Contexte, this.Sujet)) //TODO: il y a un cas foireux si le contexte est vide
                {
                    resultat = db.insertSujet(this.Contexte, this.Sujet);
                    if (resultat == 1)
                        bilan += "Nouveau sujet enregistré\n";
                }

            if (this.destHasChanged)
                if (db.isNvo(DB.destinataire, this.Destinataire))
                {
                    resultat = db.insert(DB.destinataire, this.Destinataire);
                    if (resultat == 1)
                        bilan += "Nouveau destinataire enregistré\n";
                }

            if (this.Texte != "")
            {
                if (this.isScratchpad)
                {
                    this.v_TLID = db.insertAction(this); // Sauvegarde de l'action

                    bilan += "Nouvelle action enregistrée\n";
                    if (this.hasPJ)
                    {
                        db.insertPJ(this.v_TLID, this.PJ); // Sauvegarde des PJ
                        bilan += _links.Count.ToString() + " PJ enregistrée";
                        if (_links.Count > 1) bilan += "s";
                        bilan += "\n";
                    }
                }
                else
                {
                    resultat = db.updateAction(this);
                    if (resultat == 1)
                        bilan += "Action mise à jour\n";

                    // Insertion des pj
                    List<Enclosure> added_links =
                        this._links.
                        Where(ews => ews.status == EncStatus.added).
                        Select(ews => ews.enclosure).
                        ToList<Enclosure>();

                    int nbAdded = added_links.Count;
                    if (nbAdded > 0)
                    {
                        db.insertPJ(this.v_TLID, added_links); // Sauvegarde des PJ
                        bilan += nbAdded.ToString() + " PJ enregistrée"; // Préparation du bilan
                        if (nbAdded > 1) bilan += "s";
                        bilan += "\n";
                    }

                    // Suppression des pj
                    List<Enclosure> removed_links =
                        this._links.
                        Where(ews => ews.status == EncStatus.removed).
                        Select(ews => ews.enclosure).
                        ToList<Enclosure>();

                    int nbSupp = removed_links.Count;
                    if (nbSupp > 0)
                    {
                        db.removePJ(this.v_TLID, removed_links);
                        bilan += nbSupp.ToString() + " PJ supprimée"; // Préparation du bilan
                        if (nbSupp > 1) bilan += "s";
                        bilan += "\n";
                    }

                    // Mise àjour des pj
                    List<Enclosure> updated_links =
                        this._links.
                        Where(ews => ews.status == EncStatus.renamed).
                        Select(ews => ews.enclosure).
                        ToList<Enclosure>();

                    int nbUpd = updated_links.Count;
                    if (nbUpd > 0)
                    {
                        db.renamePJ(this.v_TLID, updated_links);
                        bilan += nbUpd.ToString() + " PJ mise"; // Préparation du bilan
                        if (nbUpd > 1) bilan += "s";
                        bilan += " à jour\n";
                    }
                }
            }

            // On affiche un message de statut sur la TrayIcon
            if (bilan.Length > 0) // On n'affiche un bilan que s'il s'est passé qqchose
                TrayIcon.afficheMessage("Bilan sauvegarde", bilan.Substring(0, bilan.Length - 1)); // On supprime le dernier \n            
        }

        #region Export

        /// <summary>
        /// Export vers presse-papier à partir du template de la clé fournie
        /// </summary>
        public void clip(String key)
        {
            //Récupération des templates d'export
            NameValueCollection section = (NameValueCollection)ConfigurationManager.GetSection("Export");
            String template = section[key];

            // Remplacement des caractères spéciaux
            String resultat = template.Replace("(VIDE)", "");
            resultat = resultat.Replace("(TAB)", "\t");

            // Remplacement du sujet (Attention les sauts de ligne ne sont pas gérés)
            resultat = resultat.Replace("(Sujet)", this.v_sujt);

            // Remplacement de l'action en rentrant une formule excel pour les passages à la ligne
            String action = this.v_texte.Replace("\"", "\"\"");
            action = action.Replace(Environment.NewLine, "\"&CAR(10)&\""); // Attention compatible avec la version fr de excel seulement
            action = "=\"" + action + "\"";
            resultat = resultat.Replace("(Action)", action);

            // Remplacement du statut, de la due date et de la date courante
            resultat = resultat.Replace("(Statut)", this.v_stat);
            resultat = resultat.Replace("(DueDate)", this.v_dueDate.ToShortDateString());
            resultat = resultat.Replace("(NOW)", DateTime.Now.ToShortDateString());

            //Copie dans le presse-papier
            System.Windows.Forms.Clipboard.SetText(resultat);
        }

        #endregion
    }
}
