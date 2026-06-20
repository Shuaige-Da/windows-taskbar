namespace DynamicIslandBar
{
    public static class AudioDeviceListBuilder
    {
        public static List<AudioDevice> Build(IEnumerable<AudioDevice> devices, string? defaultDeviceId)
        {
            var result = new List<AudioDevice>();
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var device in devices)
            {
                if (string.IsNullOrWhiteSpace(device.Id) || !seenIds.Add(device.Id))
                {
                    continue;
                }

                result.Add(new AudioDevice
                {
                    Id = device.Id,
                    Name = string.IsNullOrWhiteSpace(device.Name) ? "未命名输出设备" : device.Name.Trim(),
                    IsDefault = string.Equals(device.Id, defaultDeviceId, StringComparison.OrdinalIgnoreCase)
                });
            }

            result.Sort((left, right) =>
            {
                if (left.IsDefault != right.IsDefault)
                {
                    return left.IsDefault ? -1 : 1;
                }

                return StringComparer.CurrentCultureIgnoreCase.Compare(left.Name, right.Name);
            });

            if (result.Count > 0 && result.All(device => !device.IsDefault))
            {
                result[0].IsDefault = true;
            }

            return result;
        }
    }
}
