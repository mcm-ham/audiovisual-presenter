﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:2.0.50727.3074
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

[assembly: global::System.Data.Objects.DataClasses.EdmSchemaAttribute()]
[assembly: global::System.Data.Objects.DataClasses.EdmRelationshipAttribute("SongPresenter.App_Code", "FK_Schedule_Items", "Schedules", global::System.Data.Metadata.Edm.RelationshipMultiplicity.One, typeof(SongPresenter.App_Code.Schedule), "Items", global::System.Data.Metadata.Edm.RelationshipMultiplicity.Many, typeof(SongPresenter.App_Code.Item))]

// Original file name:
// Generation date: 1/04/2009 9:46:47 p.m.
namespace SongPresenter.App_Code
{
    
    /// <summary>
    /// There are no comments for DatabaseEntities in the schema.
    /// </summary>
    public partial class DatabaseEntities : global::System.Data.Objects.ObjectContext
    {
        /// <summary>
        /// Initializes a new DatabaseEntities object using the connection string found in the 'DatabaseEntities' section of the application configuration file.
        /// </summary>
        public DatabaseEntities() : 
                base("name=DatabaseEntities", "DatabaseEntities")
        {
            this.OnContextCreated();
        }
        /// <summary>
        /// Initialize a new DatabaseEntities object.
        /// </summary>
        public DatabaseEntities(string connectionString) : 
                base(connectionString, "DatabaseEntities")
        {
            this.OnContextCreated();
        }
        /// <summary>
        /// Initialize a new DatabaseEntities object.
        /// </summary>
        public DatabaseEntities(global::System.Data.EntityClient.EntityConnection connection) : 
                base(connection, "DatabaseEntities")
        {
            this.OnContextCreated();
        }
        partial void OnContextCreated();
        /// <summary>
        /// There are no comments for Items in the schema.
        /// </summary>
        public global::System.Data.Objects.ObjectQuery<Item> Items
        {
            get
            {
                if ((this._Items == null))
                {
                    this._Items = base.CreateQuery<Item>("[Items]");
                }
                return this._Items;
            }
        }
        private global::System.Data.Objects.ObjectQuery<Item> _Items;
        /// <summary>
        /// There are no comments for Schedules in the schema.
        /// </summary>
        public global::System.Data.Objects.ObjectQuery<Schedule> Schedules
        {
            get
            {
                if ((this._Schedules == null))
                {
                    this._Schedules = base.CreateQuery<Schedule>("[Schedules]");
                }
                return this._Schedules;
            }
        }
        private global::System.Data.Objects.ObjectQuery<Schedule> _Schedules;
        /// <summary>
        /// There are no comments for Items in the schema.
        /// </summary>
        public void AddToItems(Item item)
        {
            base.AddObject("Items", item);
        }
        /// <summary>
        /// There are no comments for Schedules in the schema.
        /// </summary>
        public void AddToSchedules(Schedule schedule)
        {
            base.AddObject("Schedules", schedule);
        }
    }
    /// <summary>
    /// There are no comments for SongPresenter.App_Code.Item in the schema.
    /// </summary>
    /// <KeyProperties>
    /// ID
    /// </KeyProperties>
    [global::System.Data.Objects.DataClasses.EdmEntityTypeAttribute(NamespaceName="SongPresenter.App_Code", Name="Item")]
    [global::System.Runtime.Serialization.DataContractAttribute(IsReference=true)]
    [global::System.Serializable()]
    public partial class Item : global::System.Data.Objects.DataClasses.EntityObject
    {
        /// <summary>
        /// Create a new Item object.
        /// </summary>
        /// <param name="id">Initial value of ID.</param>
        /// <param name="filename">Initial value of Filename.</param>
        /// <param name="ordinal">Initial value of Ordinal.</param>
        public static Item CreateItem(global::System.Guid id, string filename, int ordinal)
        {
            Item item = new Item();
            item.ID = id;
            item.Filename = filename;
            item.Ordinal = ordinal;
            return item;
        }
        /// <summary>
        /// There are no comments for Property ID in the schema.
        /// </summary>
        [global::System.Data.Objects.DataClasses.EdmScalarPropertyAttribute(EntityKeyProperty=true, IsNullable=false)]
        [global::System.Runtime.Serialization.DataMemberAttribute()]
        public global::System.Guid ID
        {
            get
            {
                return this._ID;
            }
            set
            {
                this.OnIDChanging(value);
                this.ReportPropertyChanging("ID");
                this._ID = global::System.Data.Objects.DataClasses.StructuralObject.SetValidValue(value);
                this.ReportPropertyChanged("ID");
                this.OnIDChanged();
            }
        }
        private global::System.Guid _ID;
        partial void OnIDChanging(global::System.Guid value);
        partial void OnIDChanged();
        /// <summary>
        /// There are no comments for Property Filename in the schema.
        /// </summary>
        [global::System.Data.Objects.DataClasses.EdmScalarPropertyAttribute(IsNullable=false)]
        [global::System.Runtime.Serialization.DataMemberAttribute()]
        public string Filename
        {
            get
            {
                return this._Filename;
            }
            set
            {
                this.OnFilenameChanging(value);
                this.ReportPropertyChanging("Filename");
                this._Filename = global::System.Data.Objects.DataClasses.StructuralObject.SetValidValue(value, false);
                this.ReportPropertyChanged("Filename");
                this.OnFilenameChanged();
            }
        }
        private string _Filename;
        partial void OnFilenameChanging(string value);
        partial void OnFilenameChanged();
        /// <summary>
        /// There are no comments for Property Ordinal in the schema.
        /// </summary>
        [global::System.Data.Objects.DataClasses.EdmScalarPropertyAttribute(IsNullable=false)]
        [global::System.Runtime.Serialization.DataMemberAttribute()]
        public int Ordinal
        {
            get
            {
                return this._Ordinal;
            }
            set
            {
                this.OnOrdinalChanging(value);
                this.ReportPropertyChanging("Ordinal");
                this._Ordinal = global::System.Data.Objects.DataClasses.StructuralObject.SetValidValue(value);
                this.ReportPropertyChanged("Ordinal");
                this.OnOrdinalChanged();
            }
        }
        private int _Ordinal;
        partial void OnOrdinalChanging(int value);
        partial void OnOrdinalChanged();
        /// <summary>
        /// There are no comments for Schedule in the schema.
        /// </summary>
        [global::System.Data.Objects.DataClasses.EdmRelationshipNavigationPropertyAttribute("SongPresenter.App_Code", "FK_Schedule_Items", "Schedules")]
        [global::System.Xml.Serialization.XmlIgnoreAttribute()]
        [global::System.Xml.Serialization.SoapIgnoreAttribute()]
        [global::System.Runtime.Serialization.DataMemberAttribute()]
        public Schedule Schedule
        {
            get
            {
                return ((global::System.Data.Objects.DataClasses.IEntityWithRelationships)(this)).RelationshipManager.GetRelatedReference<Schedule>("SongPresenter.App_Code.FK_Schedule_Items", "Schedules").Value;
            }
            set
            {
                ((global::System.Data.Objects.DataClasses.IEntityWithRelationships)(this)).RelationshipManager.GetRelatedReference<Schedule>("SongPresenter.App_Code.FK_Schedule_Items", "Schedules").Value = value;
            }
        }
        /// <summary>
        /// There are no comments for Schedule in the schema.
        /// </summary>
        [global::System.ComponentModel.BrowsableAttribute(false)]
        [global::System.Runtime.Serialization.DataMemberAttribute()]
        public global::System.Data.Objects.DataClasses.EntityReference<Schedule> ScheduleReference
        {
            get
            {
                return ((global::System.Data.Objects.DataClasses.IEntityWithRelationships)(this)).RelationshipManager.GetRelatedReference<Schedule>("SongPresenter.App_Code.FK_Schedule_Items", "Schedules");
            }
            set
            {
                if ((value != null))
                {
                    ((global::System.Data.Objects.DataClasses.IEntityWithRelationships)(this)).RelationshipManager.InitializeRelatedReference<Schedule>("SongPresenter.App_Code.FK_Schedule_Items", "Schedules", value);
                }
            }
        }
    }
    /// <summary>
    /// There are no comments for SongPresenter.App_Code.Schedule in the schema.
    /// </summary>
    /// <KeyProperties>
    /// ID
    /// </KeyProperties>
    [global::System.Data.Objects.DataClasses.EdmEntityTypeAttribute(NamespaceName="SongPresenter.App_Code", Name="Schedule")]
    [global::System.Runtime.Serialization.DataContractAttribute(IsReference=true)]
    [global::System.Serializable()]
    public partial class Schedule : global::System.Data.Objects.DataClasses.EntityObject
    {
        /// <summary>
        /// Create a new Schedule object.
        /// </summary>
        /// <param name="id">Initial value of ID.</param>
        /// <param name="date">Initial value of Date.</param>
        /// <param name="name">Initial value of Name.</param>
        public static Schedule CreateSchedule(global::System.Guid id, global::System.DateTime date, string name)
        {
            Schedule schedule = new Schedule();
            schedule.ID = id;
            schedule.Date = date;
            schedule.Name = name;
            return schedule;
        }
        /// <summary>
        /// There are no comments for Property ID in the schema.
        /// </summary>
        [global::System.Data.Objects.DataClasses.EdmScalarPropertyAttribute(EntityKeyProperty=true, IsNullable=false)]
        [global::System.Runtime.Serialization.DataMemberAttribute()]
        public global::System.Guid ID
        {
            get
            {
                return this._ID;
            }
            set
            {
                this.OnIDChanging(value);
                this.ReportPropertyChanging("ID");
                this._ID = global::System.Data.Objects.DataClasses.StructuralObject.SetValidValue(value);
                this.ReportPropertyChanged("ID");
                this.OnIDChanged();
            }
        }
        private global::System.Guid _ID;
        partial void OnIDChanging(global::System.Guid value);
        partial void OnIDChanged();
        /// <summary>
        /// There are no comments for Property Date in the schema.
        /// </summary>
        [global::System.Data.Objects.DataClasses.EdmScalarPropertyAttribute(IsNullable=false)]
        [global::System.Runtime.Serialization.DataMemberAttribute()]
        public global::System.DateTime Date
        {
            get
            {
                return this._Date;
            }
            set
            {
                this.OnDateChanging(value);
                this.ReportPropertyChanging("Date");
                this._Date = global::System.Data.Objects.DataClasses.StructuralObject.SetValidValue(value);
                this.ReportPropertyChanged("Date");
                this.OnDateChanged();
            }
        }
        private global::System.DateTime _Date;
        partial void OnDateChanging(global::System.DateTime value);
        partial void OnDateChanged();
        /// <summary>
        /// There are no comments for Property Name in the schema.
        /// </summary>
        [global::System.Data.Objects.DataClasses.EdmScalarPropertyAttribute(IsNullable=false)]
        [global::System.Runtime.Serialization.DataMemberAttribute()]
        public string Name
        {
            get
            {
                return this._Name;
            }
            set
            {
                this.OnNameChanging(value);
                this.ReportPropertyChanging("Name");
                this._Name = global::System.Data.Objects.DataClasses.StructuralObject.SetValidValue(value, false);
                this.ReportPropertyChanged("Name");
                this.OnNameChanged();
            }
        }
        private string _Name;
        partial void OnNameChanging(string value);
        partial void OnNameChanged();
        /// <summary>
        /// There are no comments for Items in the schema.
        /// </summary>
        [global::System.Data.Objects.DataClasses.EdmRelationshipNavigationPropertyAttribute("SongPresenter.App_Code", "FK_Schedule_Items", "Items")]
        [global::System.Xml.Serialization.XmlIgnoreAttribute()]
        [global::System.Xml.Serialization.SoapIgnoreAttribute()]
        [global::System.Runtime.Serialization.DataMemberAttribute()]
        public global::System.Data.Objects.DataClasses.EntityCollection<Item> Items
        {
            get
            {
                return ((global::System.Data.Objects.DataClasses.IEntityWithRelationships)(this)).RelationshipManager.GetRelatedCollection<Item>("SongPresenter.App_Code.FK_Schedule_Items", "Items");
            }
            set
            {
                if ((value != null))
                {
                    ((global::System.Data.Objects.DataClasses.IEntityWithRelationships)(this)).RelationshipManager.InitializeRelatedCollection<Item>("SongPresenter.App_Code.FK_Schedule_Items", "Items", value);
                }
            }
        }
    }
}
