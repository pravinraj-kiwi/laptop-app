using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Client.Infrastructure.API.Mutations
{
    public class Nodes
    {
        #region Fields

        public const string CreateDeviceNode = @"
            mutation CreateDeviceNode($serialNumber: String, $type: String, $parentId: Int, $properties: jsonb = """") {
              insert_nodes_one(object: {name: $serialNumber, type: $type, parentId: $parentId, properties: $properties, hardwareId: $serialNumber}) {
                id
              }
            }";

        public const string CreateVehicleNode = @"
            mutation CreateVehicleNode($name: String, $type: String, $parentId: Int, $properties: jsonb = """") {
              insert_nodes_one(object: {name: $name, type: $type, parentId: $parentId, properties: $properties}) {
                id
              }
            }";

        public const string UpdateNodeParentId = @"
            mutation UpdateNodeParentId($nodeId: Int!, $parentId: Int) {
              update_nodes_by_pk(pk_columns: {id: $nodeId}, _set: {parentId: $parentId}) {
                id
              }
            }";

        #endregion Fields
    }
}