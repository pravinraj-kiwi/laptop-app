using System;
using System.Collections.Generic;
using System.Text;

namespace PursuitAlert.Domain.API.Queries
{
    public class Nodes
    {
        #region Fields

        public const string CreateDeviceNodeMutation = @"
            mutation CreateDeviceNodeMutation($serialNumber: String, $type: String, $parentId: Int, $properties: jsonb = """") {
              insert_nodes_one(object: {name: $serialNumber, type: $type, parentId: $parentId, properties: $properties, hardwareId: $serialNumber}) {
                id
              }
            }";

        public const string CreateVehicleNodeMutation = @"
            mutation CreateVehicleNodeMutation($name: String, $type: String, $parentId: Int, $properties: jsonb = """") {
              insert_nodes_one(object: {name: $name, type: $type, parentId: $parentId, properties: $properties}) {
                id
              }
            }";

        public const string GetDeviceNodeBySerialNumber = @"
            query GetDeviceNodeBySerialNumber($serialNumber: String) {
              nodes(where: {hardwareId: {_eq: $serialNumber}}) {
                id
                parentId
              }
            }";

        #endregion Fields
    }
}