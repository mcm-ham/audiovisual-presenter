﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:2.0.50727.4918
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

[assembly: global::System.Data.Objects.DataClasses.EdmSchemaAttribute()]
[assembly: global::System.Data.Objects.DataClasses.EdmRelationshipAttribute("SongPresenter.App_Code", "FK_Schedule_Items", "Schedules", global::System.Data.Metadata.Edm.RelationshipMultiplicity.One, typeof(SongPresenter.App_Code.Schedule), "Items", global::System.Data.Metadata.Edm.RelationshipMultiplicity.Many, typeof(SongPresenter.App_Code.Item))]
[assembly: global::System.Data.Objects.DataClasses.EdmRelationshipAttribute("SongPresenter.App_Code", "ItemFlag", "Item", global::System.Data.Metadata.Edm.RelationshipMultiplicity.One, typeof(SongPresenter.App_Code.Item), "Flag", global::System.Data.Metadata.Edm.RelationshipMultiplicity.Many, typeof(SongPresenter.App_Code.Flag))]

// Original file name:
// Generation date: 23/08/2009 2:52:37 p.m.
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
        /// There are no comments for Flags in the schema.
        /// </summary>
        public global::System.Data.Objects.ObjectQuery<Flag> Flags
        {
            get
            {
                if ((this._Flags == null))
                {
                    this._Flags = base.CreateQuery<Flag>("[Flags]");
                }
                return this._Flags;
            }
        }
        private global::System.Data.Objects.ObjectQuery<Flag> _Flags;
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
        /// <summary>
        /// There are no comments for Flags in the schema.
        /// </summary>
        public void AddToFlags(Flag flag)
        {
            base.AddObject("Flags", flag);
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
        public static Item CreateItem(global::System.Guid id, string filename, short ordinal)
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
        public short Ordinal
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
        private short _Ordinal;
        partial void OnOrdinalChanging(short value);
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
        /// <summary>
        /// There are no comments for Flags in the schema.
        /// </summary>
        [global::System.Data.Objects.DataClasses.EdmRelationshipNavigationPropertyAttribute("SongPresenter.App_Code", "ItemFlag", "Flag")]
        [global::System.Xml.Serialization.XmlIgnoreAttribute()]
        [global::System.Xml.Serialization.SoapIgnoreAttribute()]
        [global::System.Runtime.Serialization.DataMemberAttribute()]
        public global::System.Data.Objects.DataClasses.EntityCollection<Flag> Flags
        {
            get
            {
                return ((global::System.Data.Objects.DataClasses.IEntityWithRelationships)(this)).RelationshipManager.GetRelatedCollection<Flag>("SongPresenter.App_Code.ItemFlag", "Flag");
            }
            set
            {
                if ((value != null))
                {
                    ((global::System.Data.Objects.DataClasses.IEntityWithRelationships)(this)).RelationshipManager.InitializeRelatedCollection<Flag>("SongPresenter.App_Code.ItemFlag", "Flag", value);
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
    /// <summary>
    /// There are no comments for SongPresenter.App_Code.Flag in the schema.
    /// </summary>
    /// <KeyProperties>
    /// Index
    /// ItemID
    /// </KeyProperties>
    [global::System.Data.Objects.DataClasses.EdmEntityTypeAttribute(NamespaceName="SongPresenter.App_Code", Name="Flag")]
    [global::System.Runtime.Serialization.DataContractAttribute(IsReference=true)]
    [global::System.Serializable()]
    public partial class Flag : global::System.Data.Objects.DataClasses.EntityObject
    {
        /// <summary>
        /// Create a new Flag object.
        /// </summary>
        /// <param name="colour">Initial value of Colour.</param>
        /// <param name="index">Initial value of Index.</param>
        /// <param name="itemID">Initial value of ItemID.</param>
        public static Flag CreateFlag(string colour, short index, global::System.Guid itemID)
        {
            Flag flag = new Flag();
            flag.Colour = colour;
            flag.Index = index;
            flag.ItemID = itemID;
            return flag;
        }
        /// <summary>
        /// There are no comments for Property Colour in the schema.
        /// </summary>
        [global::System.Data.Objects.DataClasses.EdmScalarPropertyAttribute(IsNullable=false)]
        [global::System.Runtime.Serialization.DataMemberAttribute()]
        public string Colour
        {
            get
            {
                return this._Colour;
            }
            set
            {
                this.OnColourChanging(value);
                this.ReportPropertyChanging("Colour");
                this._Colour = global::System.Data.Objects.DataClasses.StructuralObject.SetValidValue(value, false);
                this.ReportPropertyChanged("Colour");
                this.OnColourChanged();
            }
        }
        private string _Colour;
        partial void OnColourChanging(string value);
        partial void OnColourChanged();
        /// <summary>
        /// There are no comments for Property Index in the schema.
        /// </summary>
        [global::System.Data.Objects.DataClasses.EdmScalarPropertyAttribute(EntityKeyProperty=true, IsNullable=false)]
        [global::System.Runtime.Serialization.DataMemberAttribute()]
        public short Index
        {
            get
            {
                return this._Index;
            }
            set
            {
                this.OnIndexChanging(value);
                this.ReportPropertyChanging("Index");
                this._Index = global::System.Data.Objects.DataClasses.StructuralObject.SetValidValue(value);
                this.ReportPropertyChanged("Index");
                this.OnIndexChanged();
            }
        }
        private short _Index;
        partial void OnIndexChanging(short value);
        partial void OnIndexChanged();
        /// <summary>
        /// There are no comments for Property ItemID in the schema.
        /// </summary>
        [global::System.Data.Objects.DataClasses.EdmScalarPropertyAttribute(EntityKeyProperty=true, IsNullable=false)]
        [global::System.Runtime.Serialization.DataMemberAttribute()]
        public global::System.Guid ItemID
        {
            get
            {
                return this._ItemID;
            }
            set
            {
                this.OnItemIDChanging(value);
                this.ReportPropertyChanging("ItemID");
                this._ItemID = global::System.Data.Objects.DataClasses.StructuralObject.SetValidValue(value);
                this.ReportPropertyChanged("ItemID");
                this.OnItemIDChanged();
            }
        }
        private global::System.Guid _ItemID;
        partial void OnItemIDChanging(global::System.Guid value);
        partial void OnItemIDChanged();
        /// <summary>
        /// There are no comments for Item in the schema.
        /// </summary>
        [global::System.Data.Objects.DataClasses.EdmRelationshipNavigationPropertyAttribute("SongPresenter.App_Code", "ItemFlag", "Item")]
        [global::System.Xml.Serialization.XmlIgnoreAttribute()]
        [global::System.Xml.Serialization.SoapIgnoreAttribute()]
        [global::System.Runtime.Serialization.DataMemberAttribute()]
        public Item Item
        {
            get
            {
                return ((global::System.Data.Objects.DataClasses.IEntityWithRelationships)(this)).RelationshipManager.GetRelatedReference<Item>("SongPresenter.App_Code.ItemFlag", "Item").Value;
            }
            set
            {
                ((global::System.Data.Objects.DataClasses.IEntityWithRelationships)(this)).RelationshipManager.GetRelatedReference<Item>("SongPresenter.App_Code.ItemFlag", "Item").Value = value;
            }
        }
        /// <summary>
        /// There are no comments for Item in the schema.
        /// </summary>
        [global::System.ComponentModel.BrowsableAttribute(false)]
        [global::System.Runtime.Serialization.DataMemberAttribute()]
        public global::System.Data.Objects.DataClasses.EntityReference<Item> ItemReference
        {
            get
            {
                return ((global::System.Data.Objects.DataClasses.IEntityWithRelationships)(this)).RelationshipManager.GetRelatedReference<Item>("SongPresenter.App_Code.ItemFlag", "Item");
            }
            set
            {
                if ((value != null))
                {
                    ((global::System.Data.Objects.DataClasses.IEntityWithRelationships)(this)).RelationshipManager.InitializeRelatedReference<Item>("SongPresenter.App_Code.ItemFlag", "Item", value);
                }
            }
        }
    }
}
